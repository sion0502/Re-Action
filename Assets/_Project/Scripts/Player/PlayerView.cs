using ReAction.Core;
using UnityEngine;

namespace ReAction.Player
{
    /// <summary>
    /// 플레이어 표시 담당. 로직은 건드리지 않고 PlayerMotor 상태를 읽어 애니메이션과 좌우 반전만 갱신한다.
    /// 애니메이션 속도는 월드 시간 배율을 따른다 (계획 중엔 아주 느리게, 사망 연출은 예외).
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        private const float MoveThreshold = 0.1f;
        private const string DeathState = "Death";

        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private GameController _game;

        private string _actionState;
        private string _currentState;

        private void OnEnable()
        {
            if (_game == null) return;
            _game.StateChanged += OnStateChanged;
            _game.WorldReset += OnWorldReset;
        }

        private void OnDisable()
        {
            if (_game == null) return;
            _game.StateChanged -= OnStateChanged;
            _game.WorldReset -= OnWorldReset;
        }

        private void Start()
        {
            if (_game != null) OnStateChanged(_game.State, _game.State);
        }

        /// <summary>행동이 지정한 애니메이션을 처음부터 재생한다. 비어 있으면 이동 상태 자동 선택.</summary>
        public void PlayAction(string animationState)
        {
            _actionState = string.IsNullOrEmpty(animationState) ? null : animationState;
            if (_actionState != null) Play(_actionState, restart: true);
        }

        public void ClearAction() => _actionState = null;

        private void OnStateChanged(GameState from, GameState to) => UpdateAnimatorSpeed();

        /// <summary>애니메이션도 월드 시간을 따른다 (계획 중엔 아주 느리게). 사망 연출만 예외로 정상 속도.</summary>
        private void UpdateAnimatorSpeed()
        {
            if (_game == null) return;
            _animator.speed = _game.State == GameState.Dead ? 1f : _game.WorldTimeScale;
        }

        private void OnWorldReset()
        {
            _actionState = null;
            _currentState = null;
        }

        private void LateUpdate()
        {
            if (!_animator.enabled) return; // 되감기 중에는 기록된 스프라이트를 그대로 보여준다
            UpdateAnimatorSpeed();

            _renderer.flipX = _motor.Facing < 0;
            if (_motor.IsDead)
            {
                Play(DeathState, restart: false);
                return;
            }
            Play(_actionState ?? PickLocomotionState(), restart: false);
        }

        private string PickLocomotionState()
        {
            var v = _motor.Velocity;
            if (!_motor.IsGrounded) return v.y > 0f ? "Jump" : "Fall";
            return Mathf.Abs(v.x) > MoveThreshold ? "Run" : "Idle";
        }

        private void Play(string state, bool restart)
        {
            if (!restart && state == _currentState) return;
            _currentState = state;
            _animator.Play(state, 0, 0f);
            _animator.Update(0f); // 애니메이션이 멈춰 있어도 첫 프레임은 바로 보이게
        }
    }
}
