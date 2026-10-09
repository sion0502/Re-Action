using System;

namespace ReAction.Actions
{
    /// <summary>행동에 붙는 수치의 단위. "3초 동안", "두 칸", "벽까지" 등.</summary>
    public enum AmountUnit
    {
        None,
        Seconds,
        /// <summary>거리 (1칸 = 1유닛)</summary>
        Units,
        /// <summary>막힐 때까지 ("벽까지", "끝까지"). 막히면 실패가 아니라 성공으로 끝난다.</summary>
        UntilBlocked,
    }

    /// <summary>
    /// 해석기(또는 디버그 UI)가 만든 행동 하나. 아직 검증·실행 전의 데이터.
    /// </summary>
    [Serializable]
    public struct ActionRequest
    {
        public ActionType Type;
        public Direction Direction;
        public string Target;
        public float Amount;
        public AmountUnit Unit;

        public ActionRequest(ActionType type, Direction direction = Direction.None, string target = null,
            float amount = 0f, AmountUnit unit = AmountUnit.None)
        {
            Type = type;
            Direction = direction;
            Target = target;
            Amount = amount;
            Unit = unit;
        }

        public bool HasAmount => Unit != AmountUnit.None;

        public override string ToString()
        {
            string s = Direction == Direction.None ? Type.ToString() : $"{Type}({Direction})";
            switch (Unit)
            {
                case AmountUnit.Seconds: return $"{s}[{Amount}s]";
                case AmountUnit.Units: return $"{s}[{Amount}u]";
                case AmountUnit.UntilBlocked: return $"{s}[until blocked]";
                default: return s;
            }
        }
    }
}
