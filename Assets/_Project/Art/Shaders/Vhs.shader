// 되감기·사망 연출용 VHS 전체 화면 패스 (URP Full Screen Pass Renderer Feature).
// 강도는 전역 값 _ReActionVhs (0~1)로 TimeDilationFx가 조절한다. 0이면 화면을 그대로 통과시킨다.
// 픽셀 아트가 번지지 않도록 모든 효과를 640x360 픽셀 격자 단위로 계산하고 포인트 샘플링한다.
Shader "Hidden/ReAction/Vhs"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "Vhs"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ReActionVhs;

            static const float2 Res = float2(640.0, 360.0);

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            half3 Sample(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uv).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float k = saturate(_ReActionVhs);
                if (k <= 0.001)
                    return half4(Sample(uv), 1);

                float row = floor(uv.y * Res.y);
                float frame = floor(_Time.y * 24.0); // 테이프 노이즈는 24fps로 끊어서 움직인다
                float px = 1.0 / Res.x;

                // 1) 트래킹 흔들림: 일부 줄이 가로로 1~3px 밀린다
                float jitterRow = step(0.9, Hash(float2(row, frame)));
                float shift = (Hash(float2(row * 3.1, frame)) - 0.5) * 6.0 * jitterRow;

                // 2) 찢김 띠: 화면을 타고 내려가는 두 개의 띠 안에서 크게 밀린다
                float y01 = row / Res.y;
                float band1 = step(abs(frac(y01 + _Time.y * 0.35) - 0.5), 0.025);
                float band2 = step(abs(frac(y01 * 1.7 - _Time.y * 0.22) - 0.5), 0.012);
                shift += (band1 * 10.0 + band2 * 5.0) * (Hash(float2(frame, 7.0)) * 0.6 + 0.4);

                uv.x += floor(shift * k) * px;

                // 3) 색 분리 (빨강/파랑을 정수 픽셀만큼 좌우로)
                float split = floor(1.0 + 2.0 * k) * px;
                half3 col;
                col.r = Sample(uv + float2(split, 0)).r;
                col.g = Sample(uv).g;
                col.b = Sample(uv - float2(split, 0)).b;

                // 4) 색 번짐: 채도를 조금 빼고 따뜻하게
                half luma = dot(col, half3(0.299, 0.587, 0.114));
                col = lerp(col, half3(luma * 1.05, luma, luma * 0.92), 0.35 * k);

                // 5) 주사선
                half scan = lerp(1.0, 0.82 + 0.18 * fmod(row, 2.0), k);
                col *= scan;

                // 6) 잡음: 어두운 화면에서는 고른 노이즈가 너무 세 보이므로, 아주 옅은 바탕 노이즈 + 드문 흰 점
                float2 cell = floor(uv * Res);
                half snow = Hash(cell + frame * 1.37);
                col += (snow - 0.5) * 0.025 * k;
                half speck = step(0.995, snow);
                col = lerp(col, half3(0.9, 0.9, 0.9), speck * 0.6 * k);

                // 7) 화면 아래쪽 헤드 스위칭 노이즈 띠
                float bottom = step(row, 6.0) * k;
                col = lerp(col, half3(snow, snow, snow) * 0.9, bottom * 0.7);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
