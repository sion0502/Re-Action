using System;

namespace ReAction.Actions
{
    /// <summary>
    /// 해석기(또는 디버그 UI)가 만든 행동 하나. 아직 검증·실행 전의 데이터.
    /// </summary>
    [Serializable]
    public struct ActionRequest
    {
        public ActionType Type;
        public Direction Direction;
        public string Target;

        public ActionRequest(ActionType type, Direction direction = Direction.None, string target = null)
        {
            Type = type;
            Direction = direction;
            Target = target;
        }

        public override string ToString() =>
            Direction == Direction.None ? Type.ToString() : $"{Type}({Direction})";
    }
}
