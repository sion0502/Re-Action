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
    }
}
