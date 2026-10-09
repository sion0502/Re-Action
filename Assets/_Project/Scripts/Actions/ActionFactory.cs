namespace ReAction.Actions
{
    /// <summary>
    /// ActionRequest를 실행 가능한 PlayerAction으로 바꾼다.
    /// 카탈로그에 없거나 아직 구현되지 않은 행동은 null.
    /// </summary>
    public static class ActionFactory
    {
        public static PlayerAction Create(ActionRequest request, ActionCatalog catalog)
        {
            if (catalog == null || !catalog.TryGet(request.Type, out var definition)) return null;

            switch (request.Type)
            {
                case ActionType.Run: return new RunAction(definition, request);
                case ActionType.Jump: return new JumpAction(definition, request);
                case ActionType.Roll: return new RollAction(definition, request);
                case ActionType.Wait: return new WaitAction(definition, request);
                default: return null;
            }
        }
    }
}
