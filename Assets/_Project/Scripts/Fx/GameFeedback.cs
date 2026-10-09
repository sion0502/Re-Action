using System;
using System.Collections.Generic;
using ReAction.Actions;
using ReAction.Core;
using ReAction.Enemies;
using ReAction.Stage;
using UnityEngine;

namespace ReAction.Fx
{
    /// <summary>
    /// 타격감 연출: 게임 이벤트에 맞춰 효과음, 화면 흔들림, 히트스톱을 낸다.
    /// 모두 실제 시간 기준이라 시뮬레이션 틱 결과(결정론·FINAL RUN)에는 영향이 없다.
    /// </summary>
    public sealed class GameFeedback : MonoBehaviour
    {
        [Serializable]
        public struct ClipOverride
        {
            public Sfx Sound;
            public AudioClip Clip;
        }

        [SerializeField] private GameController _game;
        [SerializeField] private PlanningController _planning;
        [SerializeField] private StageController _stage;
        [SerializeField] private CameraShake _shake;
        [SerializeField] private AudioSource _audio;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.6f;

        [Header("타격감")]
        [SerializeField] private float _killHitstop = 0.07f;
        [SerializeField] private float _kickHitstop = 0.05f;
        [SerializeField] private float _killShakePixels = 2f;
        [SerializeField] private float _kickShakePixels = 2f;
        [SerializeField] private float _glassShakePixels = 3f;
        [SerializeField] private float _deathShakePixels = 4f;

        [Tooltip("코드로 만든 효과음 대신 쓸 실제 사운드 (비우면 코드 생성음)")]
        [SerializeField] private List<ClipOverride> _overrides = new List<ClipOverride>();

        private Dictionary<Sfx, AudioClip> _clips;
        private ActionType _lastAction;

        private void Start()
        {
            _clips = ProceduralSfx.CreateAll();
            foreach (var o in _overrides)
            {
                if (o.Clip != null) _clips[o.Sound] = o.Clip;
            }

            _game.StateChanged += OnStateChanged;
            _game.ActionStarted += OnActionStarted;
            foreach (var enemy in _game.Enemies)
            {
                if (enemy != null) enemy.Died += OnEnemyDied;
            }
            foreach (var door in _stage.GetComponentsInChildren<Door>(true)) door.Opened += OnDoorOpened;
            foreach (var window in _stage.GetComponentsInChildren<BreakableWindow>(true)) window.Broken += OnWindowBroken;
            _planning.Outcome += OnPlanOutcome;
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.StateChanged -= OnStateChanged;
                _game.ActionStarted -= OnActionStarted;
            }
            if (_planning != null) _planning.Outcome -= OnPlanOutcome;
        }

        private void OnActionStarted(PlayerAction action)
        {
            _lastAction = action.Request.Type;
            switch (_lastAction)
            {
                case ActionType.Slash:
                    Play(Sfx.Slash);
                    break;
                case ActionType.WallClimb:
                case ActionType.ClimbLadder:
                    Play(Sfx.Climb);
                    break;
            }
        }

        private void OnEnemyDied(MeleeEnemy enemy)
        {
            Play(Sfx.Hit);
            _game.Hitstop(_killHitstop);
            _shake.Shake(_killShakePixels, 0.15f);
        }

        private void OnDoorOpened(Door door)
        {
            bool kicked = _lastAction == ActionType.KickDoor || _lastAction == ActionType.BreakDoor;
            if (!kicked)
            {
                Play(Sfx.DoorOpen);
                return;
            }
            Play(Sfx.DoorKick);
            _game.Hitstop(_kickHitstop);
            _shake.Shake(_kickShakePixels, 0.12f);
        }

        private void OnWindowBroken(BreakableWindow window)
        {
            Play(Sfx.Glass);
            _shake.Shake(_glassShakePixels, 0.15f);
        }

        private void OnPlanOutcome(bool executed) => Play(executed ? Sfx.Submit : Sfx.Error);

        private void OnStateChanged(GameState from, GameState to)
        {
            switch (to)
            {
                case GameState.Dead:
                    Play(Sfx.Death);
                    _shake.Shake(_deathShakePixels, 0.35f);
                    break;
                case GameState.Rewind:
                    Play(Sfx.Rewind);
                    break;
                case GameState.Cleared:
                    Play(Sfx.Clear);
                    break;
            }
        }

        private void Play(Sfx sfx)
        {
            if (_audio != null && _clips != null && _clips.TryGetValue(sfx, out var clip))
                _audio.PlayOneShot(clip, _volume);
        }
    }
}
