using ReAction.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ReAction.Fx
{
    public enum TimeFxMode
    {
        None,
        Planning,
        Dead,
        Rewind,
    }

    /// <summary>
    /// 게임 상태에 맞춰 화면 연출을 섞는다: 계획 중 시간 지연(색수차·비네트·채도·노이즈),
    /// 사망 순간 번쩍임, 되감기 VHS. 프롬프트 UI(오버레이 캔버스)에는 적용되지 않는다.
    /// 볼륨 프로필은 설정값으로 런타임에 만든다.
    /// </summary>
    public sealed class TimeDilationFx : MonoBehaviour
    {
        private static readonly int VhsIntensityId = Shader.PropertyToID("_ReActionVhs");

        [SerializeField] private GameController _game;
        [SerializeField] private TimeFxSettings _settings;

        private VolumeProfile _profile;
        private ChromaticAberration _chromatic;
        private Vignette _vignette;
        private ColorAdjustments _color;
        private FilmGrain _grain;

        private float _planningWeight;
        private float _rewindWeight;
        private float _deathFlash;

        /// <summary>잔상 등 다른 연출이 참고하는 현재 모드.</summary>
        public static TimeFxMode Mode { get; private set; }

        public static TimeFxSettings Settings { get; private set; }

        private void Awake()
        {
            Settings = _settings;

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _chromatic = _profile.Add<ChromaticAberration>(true);
            _vignette = _profile.Add<Vignette>(true);
            _color = _profile.Add<ColorAdjustments>(true);
            _grain = _profile.Add<FilmGrain>(true);
            _grain.type.Override(FilmGrainLookup.Thin1);
            volume.sharedProfile = _profile;
            Apply();
        }

        private void OnEnable() => _game.StateChanged += OnStateChanged;

        private void OnDisable()
        {
            _game.StateChanged -= OnStateChanged;
            Shader.SetGlobalFloat(VhsIntensityId, 0f);
        }

        private void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
            Mode = TimeFxMode.None;
        }

        private void OnStateChanged(GameState from, GameState to)
        {
            if (to == GameState.Dead) _deathFlash = 1f;
        }

        private void Update()
        {
            Mode = ModeFor(_game.State);

            float step = _settings.BlendSeconds > 0f ? Time.unscaledDeltaTime / _settings.BlendSeconds : 1f;
            _planningWeight = Mathf.MoveTowards(_planningWeight, Mode == TimeFxMode.Planning ? 1f : 0f, step);
            _rewindWeight = Mathf.MoveTowards(_rewindWeight, Mode == TimeFxMode.Rewind ? 1f : 0f, step * 2f);
            if (_deathFlash > 0f)
                _deathFlash = Mathf.Max(0f, _deathFlash - Time.unscaledDeltaTime / Mathf.Max(0.01f, _settings.DeathFlashSeconds));

            Apply();
        }

        private void Apply()
        {
            var s = _settings;
            float p = _planningWeight;
            float r = _rewindWeight;
            float d = _deathFlash * _deathFlash; // 처음에 세게, 빠르게 줄어든다

            _chromatic.intensity.Override(Mathf.Max(p * s.PlanningChromatic, r * s.RewindChromatic, d * s.DeathChromatic));
            _vignette.intensity.Override(Mathf.Max(p, r) * s.PlanningVignette);
            _color.saturation.Override(Mathf.Min(p * s.PlanningSaturation, r * s.RewindSaturation));
            _color.colorFilter.Override(Color.Lerp(Color.white, s.PlanningTint, p));
            _grain.intensity.Override(Mathf.Max(p * s.PlanningGrain, r * s.RewindGrain));

            // VHS는 전체 화면 패스 셰이더가 읽는 전역 값으로 조절한다 (0이면 통과).
            Shader.SetGlobalFloat(VhsIntensityId, Mathf.Max(r * s.RewindVhs, d * s.DeathVhs));
        }

        private static TimeFxMode ModeFor(GameState state)
        {
            switch (state)
            {
                case GameState.Planning:
                case GameState.Interpreting:
                    return TimeFxMode.Planning;
                case GameState.Dead: return TimeFxMode.Dead;
                case GameState.Rewind: return TimeFxMode.Rewind;
                default: return TimeFxMode.None;
            }
        }
    }
}
