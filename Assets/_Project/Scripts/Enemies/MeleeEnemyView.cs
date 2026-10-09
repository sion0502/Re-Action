using ReAction.Core;
using UnityEngine;

namespace ReAction.Enemies
{
    /// <summary>근접 적 표시. 상태에 맞는 애니메이션과 좌우 반전만 담당한다.</summary>
    public sealed class MeleeEnemyView : MonoBehaviour
    {
        [SerializeField] private MeleeEnemy _enemy;
        [SerializeField] private GameController _game;
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _renderer;

        private int _shownVersion = -1;

        private void OnEnable()
        {
            if (_game != null) _game.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (_game != null) _game.StateChanged -= OnStateChanged;
        }

        private void Start()
        {
            if (_game != null) OnStateChanged(_game.State, _game.State);
        }

        private void OnStateChanged(GameState from, GameState to) => UpdateAnimatorSpeed();

        // 적 애니메이션은 월드 시간 배율을 따른다 (계획 중엔 아주 느리게, 해석·사망 중엔 정지).
        private void UpdateAnimatorSpeed()
        {
            if (_game != null) _animator.speed = _game.WorldTimeScale;
        }

        private void LateUpdate()
        {
            if (!_animator.enabled) return; // 되감기 중에는 기록된 스프라이트를 그대로 보여준다
            UpdateAnimatorSpeed();
            _renderer.flipX = _enemy.Facing < 0;
            if (_enemy.StateVersion == _shownVersion) return;

            _shownVersion = _enemy.StateVersion;
            _animator.Play(StateName(_enemy.State), 0, 0f);
            // 멈춰 있는 동안에도 첫 프레임은 바로 보이게 한다
            _animator.Update(0f);
        }

        private static string StateName(MeleeEnemyState state)
        {
            switch (state)
            {
                case MeleeEnemyState.Chase: return "Run";
                case MeleeEnemyState.Windup: return "Attack";
                case MeleeEnemyState.Dead: return "Death";
                default: return "Idle";
            }
        }
    }
}
