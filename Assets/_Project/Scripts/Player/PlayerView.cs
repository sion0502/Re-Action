using UnityEngine;

namespace ReAction.Player
{
    /// <summary>
    /// 플레이어 표시 담당. 로직은 건드리지 않고 PlayerMotor 상태를 읽어 애니메이션과 좌우 반전만 갱신한다.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        private const float MoveThreshold = 0.1f;

        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _renderer;

        private string _actionState;
        private string _currentState;

        /// <summary>행동이 지정한 애니메이션을 처음부터 재생한다. 비어 있으면 이동 상태 자동 선택.</summary>
        public void PlayAction(string animationState)
        {
            _actionState = string.IsNullOrEmpty(animationState) ? null : animationState;
            if (_actionState != null) Play(_actionState, restart: true);
        }

        public void ClearAction() => _actionState = null;

        private void LateUpdate()
        {
            _renderer.flipX = _motor.Facing < 0;
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
        }
    }
}
