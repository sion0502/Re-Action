using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using ReAction.Actions;

namespace ReAction.Interpretation.Llm
{
    /// <summary>
    /// LLM에 보내는 시스템 프롬프트·JSON 스키마·상황 정보, 그리고 응답 해석.
    /// 시스템 프롬프트와 스키마는 허용 행동 목록만으로 정해지는 고정 텍스트라 프롬프트 캐싱이 된다
    /// (시간·요청 ID 같은 바뀌는 값을 넣지 말 것). 요청마다 바뀌는 것은 모두 user 메시지로 보낸다.
    /// </summary>
    public static class LlmPrompt
    {
        private static readonly Dictionary<ActionType, string> Descriptions = new Dictionary<ActionType, string>
        {
            { ActionType.Run, "run horizontally (default ~4 units). With no direction but a clicked room as target, run to that room." },
            { ActionType.Walk, "walk slowly (\"천천히\", \"걸어\")." },
            { ActionType.Sprint, "sprint fast (\"빨리\", \"전력 질주\", \"돌진\")." },
            { ActionType.Backstep, "step back without turning around (\"물러나\", \"후퇴\")." },
            { ActionType.Jump, "jump in place, or toward a direction when one is given." },
            { ActionType.Roll, "quick dodge roll; invulnerable while rolling (\"구르기\", \"회피\")." },
            { ActionType.Slide, "low slide, same use as a roll." },
            { ActionType.Wait, "wait in place (default 0.5s). Use with an amount for \"N초 기다려\"." },
            { ActionType.Turn, "only change facing (\"뒤돌아\", \"오른쪽 봐\"). Direction None means turn around. Free: not counted in the limit." },
            { ActionType.Slash, "sword slash in front, kills an enemy in reach (\"베어\", \"공격\", \"처치\")." },
            { ActionType.OpenDoor, "walk to a door and open it. Target: a Door zone id." },
            { ActionType.KickDoor, "kick a door open; also knocks down the enemy right behind it. Target: a Door zone id." },
            { ActionType.BreakDoor, "smash a door. Target: a Door zone id." },
            { ActionType.Enter, "go through a door/window into the room behind it. Target: the door/window just opened, or a room id." },
            { ActionType.CrashWindow, "smash a window and dive in; must be right next to it (e.g. after climbing the wall). Target: a Window zone id." },
            { ActionType.VaultWindow, "climb through an already broken window. Target: a Window zone id." },
            { ActionType.WallClimb, "climb an exterior wall up to window height and hang there. Target: an ExteriorWall zone id." },
            { ActionType.ClimbLadder, "climb a ladder up to the floor above (\"사다리\", \"위층\"), or down to the floor below with direction Down (\"내려가\", \"아래층\"). Target: a Ladder zone id." },
        };

        public static string BuildSystemPrompt(IEnumerable<ActionType> allowedActions, int maxActions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You translate a player's Korean command into actions for a 2D side-view action game.");
            sb.AppendLine("The player controls a sword fighter inside a 3-floor building and types short Korean sentences.");
            sb.AppendLine("You only interpret. The game validates and simulates everything, so never claim results.");
            sb.AppendLine();
            sb.AppendLine("Available action types:");
            foreach (var type in allowedActions.OrderBy(t => (int)t))
            {
                string description = Descriptions.TryGetValue(type, out var d) ? d : ActionNames.Get(type);
                sb.AppendLine($"- {type}: {description}");
            }
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("1. List actions in the order they appear in the sentence. One verb or clause = one action.");
            sb.AppendLine($"2. Never drop or merge actions to fit a limit. The game allows {maxActions} actions per command (Turn is free) and tells the player to shorten longer commands, so list every action you understood.");
            sb.AppendLine("3. direction: Forward = the way the character faces, Backward = behind, Left/Right = screen sides, None = unspecified. \"앞구르기\" is Roll Forward. Time words such as \"뒤에\", \"다음에\" are not directions.");
            sb.AppendLine("4. amount/unit: \"N초 (동안)\" -> Seconds; \"N칸\", \"N걸음\", \"N미터\" -> Units; \"벽까지\", \"끝까지\" -> UntilBlocked with amount 0; \"잠깐\" -> 0.5 Seconds; degree words scale the default distance/duration with unit Scale: \"아주 조금\" 0.25, \"조금\"/\"살짝\"/\"약간\"/\"짧게\" 0.5, \"멀리\"/\"많이\"/\"쭉\"/\"길게\"/\"한참\" 2, \"아주 멀리\" 3; otherwise None with amount 0.");
            sb.AppendLine("5. Repetition such as \"두 번 베어\" means repeating that action (two Slash entries).");
            sb.AppendLine("6. target: use only zone ids from the provided zones list, or \"\" when no target is needed or none fits. Prefer the clicked target_zone_id when its kind fits the action; otherwise the matching zone connected to the current zone. Never invent ids.");
            sb.AppendLine("7. If a part of the command is not an available action, or is conditional (\"적이 오면 공격\"), leave it out and explain briefly in notes. If nothing is actionable, return an empty actions list.");
            sb.AppendLine("8. notes: one short Korean sentence for the player, or \"\" when everything was understood.");
            sb.AppendLine();
            sb.AppendLine("Examples:");
            sb.AppendLine("\"문을 걷어차고 들어가서 앞구르기\" -> KickDoor(target door), Enter(target same door), Roll Forward");
            sb.AppendLine("\"3초 동안 오른쪽으로 달려\" -> Run Right, amount 3, unit Seconds");
            sb.AppendLine("\"뒤돌아서 두 번 베어\" -> Turn None, Slash, Slash");
            sb.AppendLine("\"외벽을 타고 올라가서 창문을 깨고 들어가\" -> WallClimb(target wall), CrashWindow(target window), Enter(target same window)");
            return sb.ToString();
        }

        public static JObject BuildSchema(IEnumerable<ActionType> allowedActions)
        {
            var typeNames = new JArray(allowedActions.OrderBy(t => (int)t).Select(t => t.ToString()));
            var action = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["type"] = new JObject { ["type"] = "string", ["enum"] = typeNames },
                    ["direction"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Enum.GetNames(typeof(Direction))) },
                    ["target"] = new JObject { ["type"] = "string" },
                    ["amount"] = new JObject { ["type"] = "number" },
                    ["unit"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Enum.GetNames(typeof(AmountUnit))) },
                },
                ["required"] = new JArray("type", "direction", "target", "amount", "unit"),
                ["additionalProperties"] = false,
            };
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["actions"] = new JObject { ["type"] = "array", ["items"] = action },
                    ["notes"] = new JObject { ["type"] = "string" },
                },
                ["required"] = new JArray("actions", "notes"),
                ["additionalProperties"] = false,
            };
        }

        /// <summary>요청마다 바뀌는 상황 정보 + 프롬프트. 공개된 구역만 들어간다.</summary>
        public static string BuildUserContent(InterpretRequest request)
        {
            var zones = new JArray(request.RevealedZones.Select(z => new JObject
            {
                ["id"] = z.Id,
                ["kind"] = z.Kind.ToString(),
                ["floor"] = z.Floor,
                ["connections"] = new JArray(z.Connections),
                ["affordances"] = new JArray(z.Affordances.Select(a => a.ToString())),
            }));
            var context = new JObject
            {
                ["current_zone_id"] = request.CurrentZoneId ?? "",
                ["target_zone_id"] = request.TargetZoneId ?? "",
                ["zones"] = zones,
                ["command"] = request.Prompt ?? "",
            };
            return context.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>LLM이 돌려준 JSON을 InterpretResult로. 모르는 값은 버리고 notes에 남긴다 (검증은 ActionValidator가 한다).</summary>
        public static InterpretResult ParseResult(string json, int maxActions)
        {
            var root = JObject.Parse(json);
            var result = new InterpretResult { Raw = json, Notes = (string)root["notes"] ?? "" };
            var dropped = new List<string>();

            foreach (var item in (root["actions"] as JArray ?? new JArray()).OfType<JObject>())
            {
                string typeName = (string)item["type"];
                if (!Enum.TryParse(typeName, true, out ActionType type) || type == ActionType.None)
                {
                    dropped.Add(typeName);
                    continue;
                }

                Enum.TryParse((string)item["direction"], true, out Direction direction);
                // 문·창문·벽·사다리·진입은 대상 위치로 방향이 정해진다. 모델이 붙인 방향은 무시한다.
                if ((ActionTargeting.RequiredKind(type).HasValue || type == ActionType.Enter) &&
                    !(type == ActionType.ClimbLadder && (direction == Direction.Up || direction == Direction.Down)))
                    direction = Direction.None;
                Enum.TryParse((string)item["unit"], true, out AmountUnit unit);
                float amount = item["amount"]?.Type == JTokenType.Float || item["amount"]?.Type == JTokenType.Integer
                    ? (float)item["amount"]
                    : 0f;
                // 시간·거리 단위인데 값이 0 이하면 수치가 없는 것으로 본다 (모델이 단위만 채우는 경우가 있다).
                if ((unit == AmountUnit.Seconds || unit == AmountUnit.Units || unit == AmountUnit.Scale) && amount <= 0f)
                    unit = AmountUnit.None;
                if (unit == AmountUnit.None) amount = 0f;
                string target = (string)item["target"];

                result.Actions.Add(new ActionRequest(type, direction, string.IsNullOrWhiteSpace(target) ? null : target,
                    amount, unit));
            }

            if (dropped.Count > 0)
                result.Notes = (result.Notes + $" (알 수 없는 행동 무시: {string.Join(", ", dropped)})").Trim();
            result.OverLimit = ActionRules.CountTowardLimit(result.Actions.Select(a => a.Type)) > maxActions;
            if (result.Actions.Count == 0 && string.IsNullOrEmpty(result.Notes)) result.Notes = "어떤 행동인지 알 수 없다";
            return result;
        }
    }
}
