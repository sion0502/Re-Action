using UnityEngine;

namespace ReAction.Enemies
{
    /// <summary>근접 적 수치. 반응 타이밍은 모두 고정값이다 (결정론).</summary>
    [CreateAssetMenu(menuName = "ReAction/Melee Enemy Settings", fileName = "MeleeEnemy")]
    public sealed class MeleeEnemySettings : ScriptableObject
    {
        [Tooltip("이 거리 안, 같은 구역에 플레이어가 있으면 알아챈다 (유닛)")]
        public float AggroRange = 6f;
        [Tooltip("같은 높이로 보는 수직 차이 (유닛)")]
        public float VerticalTolerance = 1f;
        [Tooltip("추격 속도 (유닛/초)")]
        public float MoveSpeed = 2.5f;
        [Tooltip("이 거리 안이면 공격을 시작한다 (유닛)")]
        public float AttackStartRange = 1f;
        [Tooltip("공격 판정 사거리 (유닛)")]
        public float StrikeRange = 1.3f;
        [Tooltip("공격 시작부터 판정까지 (초). 이 사이에 구르기·베기로 대응할 수 있다")]
        public float WindupSeconds = 0.45f;
        [Tooltip("공격 후 다시 움직이기까지 (초)")]
        public float RecoverSeconds = 0.6f;
    }
}
