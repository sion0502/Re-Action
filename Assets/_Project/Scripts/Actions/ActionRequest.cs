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
        /// <summary>
        /// 기본 거리·시간의 배수 ("조금" 0.5, "멀리" 2). 걷기·달리기·질주는 각자의 기본 거리에 곱하므로 서로 차이가 유지된다.
        /// 에셋·기록에 정수로 저장되므로 새 값은 항상 맨 뒤에 추가한다.
        /// </summary>
        Scale,
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

        /// <summary>기본 거리·시간에 곱할 배수. "조금"·"멀리" 같은 말이 없으면 1.</summary>
        public float Scale => Unit == AmountUnit.Scale ? Amount : 1f;

        public override string ToString()
        {
            string s = Direction == Direction.None ? Type.ToString() : $"{Type}({Direction})";
            switch (Unit)
            {
                case AmountUnit.Seconds: return $"{s}[{Amount}s]";
                case AmountUnit.Units: return $"{s}[{Amount}u]";
                case AmountUnit.UntilBlocked: return $"{s}[until blocked]";
                case AmountUnit.Scale: return $"{s}[x{Amount}]";
                default: return s;
            }
        }
    }
}
