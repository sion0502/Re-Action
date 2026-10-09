using UnityEngine;

namespace ReAction.Fx
{
    /// <summary>시간 지연·사망·되감기 연출 수치. 모두 연출 전용이라 게임 판정에 영향이 없다.</summary>
    [CreateAssetMenu(menuName = "ReAction/Time Fx Settings", fileName = "TimeFx")]
    public sealed class TimeFxSettings : ScriptableObject
    {
        [Header("계획 중 (시간이 아주 느리게 흐를 때)")]
        [Range(0f, 1f)] public float PlanningChromatic = 0.25f;
        [Range(0f, 1f)] public float PlanningVignette = 0.3f;
        [Range(-100f, 0f)] public float PlanningSaturation = -30f;
        public Color PlanningTint = new Color(0.85f, 0.92f, 1f, 1f);
        [Tooltip("URP 필름 그레인은 화면 해상도 단위라 픽셀 아트와 안 맞는다. 아주 약하게만")]
        [Range(0f, 1f)] public float PlanningGrain = 0f;
        [Tooltip("연출이 켜지고 꺼지는 데 걸리는 시간 (초)")]
        public float BlendSeconds = 0.25f;

        [Header("사망 순간")]
        [Range(0f, 1f)] public float DeathChromatic = 0.8f;
        [Range(0f, 1f)] public float DeathVhs = 0.35f;
        public float DeathFlashSeconds = 0.5f;

        [Header("되감기 (VHS)")]
        [Range(0f, 1f)] public float RewindVhs = 0.6f;
        [Range(0f, 1f)] public float RewindChromatic = 0.5f;
        [Range(-100f, 0f)] public float RewindSaturation = -45f;
        [Range(0f, 1f)] public float RewindGrain = 0.05f;

        [Header("잔상")]
        [Tooltip("계획 중 이만큼(픽셀) 움직일 때마다 떠나온 자세가 잔상으로 남는다")]
        [Min(1)] public int GhostMinMovePixels = 1;
        [Tooltip("잔상 하나가 남아 있는 시간 (초). 먼저 생긴 잔상부터 사라진다")]
        public float PlanningGhostSeconds = 2.5f;
        [Tooltip("잔상이 수명의 이 비율까지는 진하게 유지되다가 그 뒤로 서서히 사라진다")]
        [Range(0f, 0.95f)] public float GhostHoldFraction = 0.4f;
        [Range(0f, 1f)] public float GhostAlpha = 0.4f;
        [Tooltip("잔상의 빨강/청록 분리 최대 거리 (픽셀)")]
        public int GhostSplitPixels = 2;
    }
}
