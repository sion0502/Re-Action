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
        [Range(0f, 1f)] public float RewindVhs = 1f;
        [Range(0f, 1f)] public float RewindChromatic = 0.5f;
        [Range(-100f, 0f)] public float RewindSaturation = -45f;
        [Range(0f, 1f)] public float RewindGrain = 0.12f;

        [Header("잔상")]
        [Tooltip("실행 중 움직임을 기억해 두는 간격 (초). 계획에 들어가는 순간 이 궤적이 잔상으로 남는다")]
        public float HistoryInterval = 0.035f;
        public int HistoryGhosts = 6;
        public float HistoryGhostSeconds = 1.4f;
        [Tooltip("계획 중 현재 자세에서 잔상이 피어나는 간격 / 수명 (초)")]
        public float PlanningGhostInterval = 0.22f;
        public float PlanningGhostSeconds = 1.1f;
        [Tooltip("되감기 중 잔상 간격 / 수명 (초)")]
        public float RewindGhostInterval = 0.04f;
        public float RewindGhostSeconds = 0.3f;
        [Range(0f, 1f)] public float GhostAlpha = 0.4f;
        [Tooltip("잔상의 빨강/청록 분리 최대 거리 (픽셀)")]
        public int GhostSplitPixels = 2;
    }
}
