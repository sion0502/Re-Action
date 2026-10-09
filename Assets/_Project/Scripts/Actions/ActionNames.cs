using System.Collections.Generic;

namespace ReAction.Actions
{
    /// <summary>
    /// 플레이어에게 보여줄 행동·방향의 한국어 이름 (미리보기, 오류 메시지).
    /// </summary>
    public static class ActionNames
    {
        private static readonly Dictionary<ActionType, string> Names = new Dictionary<ActionType, string>
        {
            { ActionType.Walk, "걷기" },
            { ActionType.Run, "달리기" },
            { ActionType.Sprint, "전력 질주" },
            { ActionType.Backstep, "뒤로 물러나기" },
            { ActionType.Jump, "점프" },
            { ActionType.DoubleJump, "이단 점프" },
            { ActionType.Slide, "슬라이딩" },
            { ActionType.Roll, "구르기" },
            { ActionType.Backflip, "백플립" },
            { ActionType.Crouch, "웅크리기" },
            { ActionType.WallClimb, "벽 타기" },
            { ActionType.WallJump, "벽 점프" },
            { ActionType.GrabLedge, "턱 잡기" },
            { ActionType.ClimbLedge, "턱 오르기" },
            { ActionType.VaultWindow, "창문 넘기" },
            { ActionType.CrashWindow, "창문 깨고 진입" },
            { ActionType.ClimbLadder, "사다리 오르기" },
            { ActionType.ExteriorMove, "외벽 이동" },
            { ActionType.HangCeiling, "천장 매달리기" },
            { ActionType.Slash, "베기" },
            { ActionType.Stab, "찌르기" },
            { ActionType.Kick, "발차기" },
            { ActionType.Shove, "밀치기" },
            { ActionType.JumpAttack, "점프 공격" },
            { ActionType.RollAttack, "구르며 공격" },
            { ActionType.Disarm, "무기 빼앗기" },
            { ActionType.Throw, "던지기" },
            { ActionType.OpenDoor, "문 열기" },
            { ActionType.KickDoor, "문 걷어차기" },
            { ActionType.BreakDoor, "문 부수기" },
            { ActionType.Enter, "진입" },
            { ActionType.PressButton, "버튼 누르기" },
            { ActionType.PickUp, "줍기" },
            { ActionType.ThrowObject, "물건 던지기" },
            { ActionType.UseExplosive, "폭발물 사용" },
            { ActionType.Wait, "대기" },
            { ActionType.Turn, "방향 전환" },
        };

        public static string Get(ActionType type) =>
            Names.TryGetValue(type, out var name) ? name : type.ToString();

        public static string Get(Direction direction)
        {
            switch (direction)
            {
                case Direction.Forward: return "앞으로";
                case Direction.Backward: return "뒤로";
                case Direction.Left: return "왼쪽으로";
                case Direction.Right: return "오른쪽으로";
                default: return "";
            }
        }

        /// <summary>예: "앞으로 구르기", "문 걷어차기"</summary>
        public static string Describe(ActionRequest request)
        {
            if (request.Type == ActionType.Turn)
            {
                return request.Direction == Direction.None || request.Direction == Direction.Backward
                    ? "뒤돌기"
                    : $"{Get(request.Direction)} 돌기";
            }

            string direction = Get(request.Direction);
            string amount = DescribeAmount(request);
            string name = Get(request.Type);
            if (amount.Length > 0) name = $"{amount} {name}";
            return direction.Length == 0 ? name : $"{direction} {name}";
        }

        /// <summary>예: "3초 동안", "2칸", "막힐 때까지"</summary>
        public static string DescribeAmount(ActionRequest request)
        {
            switch (request.Unit)
            {
                case AmountUnit.Seconds: return $"{FormatNumber(request.Amount)}초 동안";
                case AmountUnit.Units: return $"{FormatNumber(request.Amount)}칸";
                case AmountUnit.UntilBlocked: return "막힐 때까지";
                default: return "";
            }
        }

        private static string FormatNumber(float value) =>
            System.Math.Abs(value - System.Math.Round(value)) < 0.01f
                ? ((int)System.Math.Round(value)).ToString()
                : value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// 계획 요약. 제한에 세는 행동만 번호를 붙인다.
        /// 예: "뒤돌기 → [1] 구르기 → [2] 점프"
        /// </summary>
        public static string DescribePlan(IEnumerable<ActionRequest> actions)
        {
            var parts = new List<string>();
            int number = 0;
            foreach (var a in actions)
            {
                parts.Add(ActionRules.CountsTowardLimit(a.Type) ? $"[{++number}] {Describe(a)}" : Describe(a));
            }
            return string.Join(" → ", parts);
        }
    }
}
