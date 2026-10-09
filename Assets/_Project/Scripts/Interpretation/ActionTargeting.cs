using ReAction.Actions;
using ReAction.Stage;

namespace ReAction.Interpretation
{
    /// <summary>
    /// 행동마다 대상으로 삼아야 하는 구역 종류. 해석기(대상 추정)와 검증기(대상 확인)가 같이 쓴다.
    /// </summary>
    public static class ActionTargeting
    {
        /// <summary>대상 구역이 꼭 필요한 행동이면 그 종류, 아니면 null.</summary>
        public static ZoneKind? RequiredKind(ActionType type)
        {
            switch (type)
            {
                case ActionType.OpenDoor:
                case ActionType.KickDoor:
                case ActionType.BreakDoor:
                    return ZoneKind.Door;
                case ActionType.CrashWindow:
                case ActionType.VaultWindow:
                    return ZoneKind.Window;
                case ActionType.WallClimb:
                case ActionType.ExteriorMove:
                    return ZoneKind.ExteriorWall;
                case ActionType.ClimbLadder:
                    return ZoneKind.Ladder;
                default:
                    return null;
            }
        }

        /// <summary>대상 출입구(문·창문)를 지나갈 수 있게 만드는 행동인지. 뒤따르는 진입의 전제 조건.</summary>
        public static bool OpensEntry(ActionType type) =>
            type == ActionType.OpenDoor || type == ActionType.KickDoor || type == ActionType.BreakDoor ||
            type == ActionType.CrashWindow || type == ActionType.VaultWindow;

        /// <summary>대상 종류를 한국어로 ("그곳에는 문이 없다" 같은 메시지용).</summary>
        public static string KindName(ZoneKind kind)
        {
            switch (kind)
            {
                case ZoneKind.Door: return "문";
                case ZoneKind.Window: return "창문";
                case ZoneKind.ExteriorWall: return "탈 수 있는 벽";
                case ZoneKind.Ladder: return "사다리";
                default: return "그런 것";
            }
        }
    }
}
