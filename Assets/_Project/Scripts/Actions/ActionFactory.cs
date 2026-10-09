using ReAction.Stage;

namespace ReAction.Actions
{
    /// <summary>
    /// ActionRequest를 실행 가능한 PlayerAction으로 바꾼다.
    /// 카탈로그에 없거나 아직 구현되지 않은 행동은 null.
    /// </summary>
    public static class ActionFactory
    {
        public static PlayerAction Create(ActionRequest request, ActionCatalog catalog, IStageWorld world = null)
        {
            if (catalog == null || !catalog.TryGet(request.Type, out var definition)) return null;

            var action = CreateAction(request, definition);
            if (action != null) action.World = world;
            return action;
        }

        private static PlayerAction CreateAction(ActionRequest request, ActionDefinition definition)
        {
            switch (request.Type)
            {
                case ActionType.Run:
                case ActionType.Walk:
                case ActionType.Sprint:
                    return new RunAction(definition, request);
                case ActionType.Backstep: return new BackstepAction(definition, request);
                case ActionType.Jump: return new JumpAction(definition, request);
                case ActionType.Roll:
                case ActionType.Slide:
                    return new RollAction(definition, request);
                case ActionType.Wait: return new WaitAction(definition, request);
                case ActionType.Turn: return new TurnAction(definition, request);
                case ActionType.Slash: return new SlashAction(definition, request);
                case ActionType.OpenDoor:
                case ActionType.KickDoor:
                case ActionType.BreakDoor:
                    return new DoorAction(definition, request);
                case ActionType.Enter: return new EnterAction(definition, request);
                case ActionType.CrashWindow:
                case ActionType.VaultWindow:
                    return new WindowAction(definition, request);
                case ActionType.WallClimb: return new WallClimbAction(definition, request);
                case ActionType.ClimbLadder: return new LadderAction(definition, request);
                default: return null;
            }
        }
    }
}
