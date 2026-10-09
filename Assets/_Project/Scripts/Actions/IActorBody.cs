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
    }
}
