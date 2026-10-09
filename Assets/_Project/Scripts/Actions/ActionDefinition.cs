using UnityEngine;

namespace ReAction.Actions
{
    /// <summary>
    /// 행동 하나의 설정값. 수치·표시 이름·애니메이션 매핑을 코드 밖에서 조정한다.
    /// 행동마다 쓰는 필드만 의미가 있다 (예: Wait는 Duration만 사용).
    /// </summary>
    [CreateAssetMenu(menuName = "ReAction/Action Definition", fileName = "Action_")]
    public sealed class ActionDefinition : ScriptableObject
    {
        [SerializeField] private ActionType _type;
        [SerializeField] private string _displayName;

        [Tooltip("실행 중 재생할 Animator 상태 이름. 비우면 이동 상태(Idle/Run/Jump/Fall)를 자동으로 고른다.")]
        [SerializeField] private string _animationState;

        [Header("Tuning")]
        [Tooltip("수평 이동 속도 (유닛/초)")]
        [SerializeField] private float _speed;
        [Tooltip("수직 속도 (유닛/초). 점프에 사용")]
        [SerializeField] private float _verticalSpeed;
        [Tooltip("이동 거리 (유닛). 0이면 거리 제한 없음")]
        [SerializeField] private float _distance;
        [Tooltip("지속 시간 (초). 0이면 행동 고유 종료 조건을 따름")]
        [SerializeField] private float _duration;
        [Tooltip("이 시간(초) 안에 끝나지 않으면 강제로 종료한다")]
        [SerializeField] private float _timeout = 3f;

        public ActionType Type => _type;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _type.ToString() : _displayName;
        public string AnimationState => _animationState;
        public float Speed => _speed;
        public float VerticalSpeed => _verticalSpeed;
        public float Distance => _distance;
        public float Duration => _duration;
        public float Timeout => _timeout;

#if UNITY_EDITOR
        public void EditorSetup(ActionType type, string displayName, string animationState,
            float speed, float verticalSpeed, float distance, float duration, float timeout)
        {
            _type = type;
            _displayName = displayName;
            _animationState = animationState;
            _speed = speed;
            _verticalSpeed = verticalSpeed;
            _distance = distance;
            _duration = duration;
            _timeout = timeout;
        }
#endif
    }
}
