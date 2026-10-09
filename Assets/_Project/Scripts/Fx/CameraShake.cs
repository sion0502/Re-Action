using UnityEngine;

namespace ReAction.Fx
{
    /// <summary>
    /// 화면 흔들림. 픽셀 아트가 번지지 않도록 카메라를 정수 픽셀 단위로만 움직인다. 연출 전용.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        private const int PixelsPerUnit = 32;
        // 화면이 자주 바뀌어 보이지 않도록 흔드는 위치는 이 간격(초)마다 바꾼다
        private const float JitterInterval = 1f / 30f;

        private Vector3 _basePosition;
        private float _strengthPixels;
        private float _duration;
        private float _elapsed;
        private float _jitterTimer;
        private System.Random _random = new System.Random(7);

        private void Awake() => _basePosition = transform.localPosition;

        /// <summary>최대 strengthPixels 픽셀만큼, duration초 동안 줄어들며 흔든다. 더 센 흔들림이 우선한다.</summary>
        public void Shake(float strengthPixels, float duration)
        {
            float remaining = _duration > 0f ? _strengthPixels * (1f - _elapsed / _duration) : 0f;
            if (strengthPixels < remaining) return;
            _strengthPixels = strengthPixels;
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;
            _jitterTimer = 0f;
        }

        private void LateUpdate()
        {
            if (_duration <= 0f) return;

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= _duration)
            {
                _duration = 0f;
                transform.localPosition = _basePosition;
                return;
            }

            _jitterTimer -= Time.unscaledDeltaTime;
            if (_jitterTimer > 0f) return;
            _jitterTimer = JitterInterval;

            float amplitude = _strengthPixels * (1f - _elapsed / _duration);
            int px = Mathf.RoundToInt(((float)_random.NextDouble() * 2f - 1f) * amplitude);
            int py = Mathf.RoundToInt(((float)_random.NextDouble() * 2f - 1f) * amplitude);
            transform.localPosition = _basePosition + new Vector3(px, py, 0f) / PixelsPerUnit;
        }
    }
}
