using UnityEngine;

namespace ReAction.Actions
{
    /// <summary>
    /// 행동이 조작하는 대상. 행동 로직을 MonoBehaviour와 분리하기 위한 경계.
    /// </summary>
    public interface IActorBody
    {
        Vector2 Position { get; }
        Vector2 Velocity { get; set; }
        bool IsGrounded { get; }

        /// <summary>바라보는 방향. -1(왼쪽) 또는 1(오른쪽).</summary>
        int Facing { get; set; }

        /// <summary>피격 무시 (구르기 중 등).</summary>
        bool Invulnerable { get; set; }

        /// <summary>이 몸체가 속한 물리 씬. 공격 판정 같은 물리 질의는 여기서 한다 (테스트용 별도 씬 지원).</summary>
        PhysicsScene2D PhysicsScene { get; }
    }
}
