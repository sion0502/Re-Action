namespace ReAction.Actions
{
    /// <summary>
    /// 게임이 허용하는 행동 전체 목록. 새 행동 추가 체크리스트는 CLAUDE.md 12장 참고.
    /// </summary>
    public enum ActionType
    {
        None = 0,

        // 이동
        Walk, Run, Sprint, Backstep, Jump, DoubleJump, Slide, Roll, Backflip, Crouch,

        // 이동 응용
        WallClimb, WallJump, GrabLedge, ClimbLedge, VaultWindow, CrashWindow, ClimbLadder, ExteriorMove, HangCeiling,

        // 전투
        Slash, Stab, Kick, Shove, JumpAttack, RollAttack, Disarm, Throw,

        // 환경 상호작용
        OpenDoor, KickDoor, BreakDoor, Enter, PressButton, PickUp, ThrowObject, UseExplosive, Wait,

        // 무료 지시 (행동 수 제한에 세지 않음). 에셋에 정수로 저장되므로 새 값은 항상 맨 뒤에 추가한다.
        Turn,
    }

    public static class ActionRules
    {
        /// <summary>프롬프트당 최대 3개 제한에 세는 행동인지. 방향 전환은 세지 않는다.</summary>
        public static bool CountsTowardLimit(ActionType type) => type != ActionType.Turn && type != ActionType.None;

        public static int CountTowardLimit(System.Collections.Generic.IEnumerable<ActionType> types)
        {
            int count = 0;
            foreach (var t in types)
            {
                if (CountsTowardLimit(t)) count++;
            }
            return count;
        }
    }
}
