# CLAUDE.md — 프로젝트 가이드 (가제: PROMPT ZERO)

> 이 파일은 Claude Code가 이 프로젝트에서 작업할 때 가장 먼저 읽는 기준 문서다.
> 기획 의도, 핵심 시스템, 기술 구조, 개발 순서를 정의한다. 모호한 부분이 생기면 이 문서의 **핵심 원칙**을 우선한다.
> `AI_액션게임_개발기획서.md`는 **참고용**이다. 이 문서와 내용이 다르면 항상 이 문서(CLAUDE.md)를 따른다. 기획서는 이 문서에 없는 세부 아이디어를 참고할 때만 본다.

---

## 1. 게임 한 줄 요약

플레이어는 캐릭터를 직접 조종하지 않는다. **마우스로 "어디서"를, 자연어 프롬프트로 "어떻게"를 지정**하고, 죽고 다시 시작하며 건물 구조를 외워서, 3층 건물 꼭대기까지 가는 자기만의 완벽한 액션 시퀀스를 만든다.

- 장르: 2D 사이드뷰 액션 / 반복·암기형 루트 최적화 (레퍼런스: *Katana ZERO*)
- 핵심 루프: **탐색 → 죽음 → 암기 → 프롬프트 수정 → 최적화 → 완벽한 런**
- 용도: AI 활용 게임 경진대회 출품작 (짧고 임팩트 있는 1 스테이지 데모가 목표)

---

## 2. 핵심 원칙 (모든 판단의 기준)

1. **입력은 자연어 + 마우스뿐이다.** 이동/공격 키 조작은 없다. (디버그용 키는 예외)
2. **한 번의 프롬프트 = 최대 3개의 행동.** 1~3개 허용, 4개 이상이면 **아무것도 실행하지 않고** 줄여서 다시 입력하라고 안내한다. (임의로 3개로 자르지 않는다) **방향 전환("뒤돌아", "오른쪽 봐" → `Turn`)은 개수에 세지 않는 무료 지시**다. "왼쪽으로 달려"처럼 행동에 붙은 방향어도 별도로 세지 않는다.
3. **행동이 끝나면 세계는 아주 느려진다 (완전 정지 아님).** 계획(Planning) 중에는 시간이 정상의 약 1%(설정값)로 흐른다. 적도 느리게 다가오며, 입력을 오래 고민하면 실제로 불리해진다. 이 동안 트레일·고스팅·색수차 잔상 같은 "시간 지연" 연출을 보여준다 (잔상은 프롬프트 입력 중에 **실제로 움직인 것에만** 남는다. 실행 중의 움직임이나 가만히 있는 대상에는 잔상이 없다. 먼저 생긴 잔상부터 사라지고, 실행·사망·되감기로 넘어가면 즉시 지운다). 단, **해석 중(AI 응답 대기)에는 완전히 정지**한다 (LLM 지연이 불리하게 작용하지 않도록).
4. **죽으면 처음부터.** 데스 패널티 없음. 사망 연출 후 **"다시 시작" 버튼(또는 Enter)을 눌러야** 재시작한다(자동 리셋 아님). 누르면 그 런의 행동이 **뒤로 되감기는 연출**을 보여준 뒤 시작 지점에서 다시 Planning. 되감기는 런 길이와 상관없이 짧게(기본 약 1.5초, 설정값) 압축하고 클릭으로 건너뛸 수 있다.
5. **결정론(Deterministic).** 같은 입력 시퀀스는 항상 같은 결과를 낸다. 적 배치·AI 행동에 랜덤이 있으면 시드 고정. (암기 플레이와 최종 리플레이의 전제 조건) 계획 중에도 시간이 흐르므로 **"입력 시퀀스" = 각 프롬프트의 행동 + 그 프롬프트가 실행된 틱 번호**다. 느린 시간도 고정 틱으로만 진행하고, RunRecord에는 실행 틱을 함께 기록한다.
6. **AI는 해석만 하고, 판정은 게임 코드가 한다.** LLM 출력은 절대 신뢰하지 않고 항상 검증 레이어를 거친다.
7. **LLM 없이도 게임이 돌아가야 한다.** 오프라인 목업 파서(Mock)가 항상 존재해야 한다.

---

## 3. 기술 스택

| 항목 | 선택 |
|---|---|
| 엔진 | Unity 2022.3 LTS 이상 (또는 Unity 6), 2D (URP 2D 권장) |
| 언어 | C# |
| 입력 | Unity Input System (새 입력 시스템) |
| UI | uGUI + TextMeshPro. 한글 폰트는 Galmuri11 픽셀 폰트(SIL OFL, `Art/Fonts/`), TMP 래스터 모드 + 크기 11 + 캔버스 기준 640×360. 입력창 텍스트는 `extraPadding`을 꺼야 글자가 안 깨진다 |
| JSON | `com.unity.nuget.newtonsoft-json` |
| HTTP | `UnityWebRequest` + async/await (또는 UniTask 도입 시 문서에 기록) |
| AI | 현재 기본: **Ollama 로컬 모델** (`richardyoung/qwen3-14b-abliterated:Q5_K_M`, API 키·비용 없음, 이 PC에서 Ollama가 켜져 있어야 함). **Claude** (`claude-opus-5-5`)로도 전환 가능. 둘 다 `UnityWebRequest`로 직접 호출하고 `IActionInterpreter` / `ILlmProvider`로 추상화. 설정: `ScriptableObjects/LlmSettings.asset`의 `Provider` |

### Claude Code 작업 시 주의 (Unity MCP 연동)
이 프로젝트는 **Unity MCP 서버로 Claude Code가 Unity 에디터를 직접 조작**한다.

- 작업 시작 시 MCP 연결 상태와 사용 가능한 도구 목록을 먼저 확인한다. 연결이 안 되어 있으면 작업을 멈추고 사용자에게 알린다 (에디터가 열려 있는지, MCP 브리지가 실행 중인지).
- **씬 구성, GameObject 생성, 컴포넌트 추가·설정, 프리팹·ScriptableObject 에셋 생성, 플레이 모드 실행**은 MCP 도구로 직접 한다. 사용자에게 수작업을 넘기지 않는다.
- **스크립트 작성 → 컴파일 확인 → 에디터 작업** 순서를 지킨다. C# 파일을 수정한 뒤에는 MCP로 컴파일 완료/에러를 확인하고, 콘솔 에러가 0개일 때만 다음 단계로 넘어간다.
- 기능을 만든 뒤에는 **플레이 모드로 직접 실행해 콘솔 로그로 검증**한다 (필요하면 `Debug.Log`에 `[PZ]` 접두사를 붙여 필터링). EditMode 테스트가 있으면 MCP로 실행한다.
- 스테이지처럼 반복 생성·수정이 잦은 것은 MCP로 손으로 배치하기보다 **데이터(`StageDefinition`) + 빌더 코드**로 만들고, MCP로는 빌더를 실행·검증한다. 그래야 결과가 재현 가능하고 diff로 추적된다.
  - 현재 빌더: 메뉴 `ReAction/Build Stage 1 Scene` (`Assets/Editor/StageSceneBuilder.cs`). 스테이지 1 데이터는 `Assets/Editor/Stage1Data.cs` (구역·지형·적 배치·카메라 위치). 문·창문은 구역 데이터에서 자동으로 만들어진다.
  - 빌더에서 새로 만든 에셋은 씬을 열기(`OpenScene`) **전에** `AssetDatabase.SaveAssets()`로 저장하고, 씬을 연 뒤 다시 불러서 참조한다. 저장 안 된 새 에셋은 씬 전환 때 언로드되어 참조가 조용히 빠진다.
  - 시간 지연·VHS 연출: 수치는 `ScriptableObjects/Fx/TimeFx.asset`, 볼륨은 `TimeDilationFx`가 런타임에 만들고, VHS는 `Renderer2D`의 Full Screen Pass(`Art/Shaders/Vhs.shader`, 전역 값 `_ReActionVhs`로 강도 조절). 연출은 판정에 영향을 주지 않는다. URP 필름 그레인은 화면 해상도 단위라 픽셀 아트와 안 맞으므로 거의 쓰지 않고, 노이즈는 셰이더에서 640×360 격자 단위로 만든다. 잔상(`AfterimageTrail`)은 원본 바로 아래 순서로 그려서 안개에 가려진 적의 잔상이 새지 않게 한다.
  - 물리 질의는 `Physics2D.*` 대신 몸체의 `PhysicsScene`으로 한다. 결정론 테스트는 열린 씬을 건드리지 않도록 미리보기 씬(별도 물리 씬)에서 `WorldSimulation`을 돌린다.
- 에디터 창에 포커스가 없으면 플레이 모드 프레임이 거의 진행되지 않는다. 검증할 때는 `EditorApplication.isPaused = true` 후 `EditorApplication.Step()`으로 프레임을 직접 넘긴다. 이 방식에서는 마우스 클릭(`wasPressedThisFrame`)이 전달되지 않으므로, 입력 처리는 `ZoneSelector.ClickAt`처럼 직접 호출 가능한 메서드로 분리해 둔다. 검증 후 `isPaused = false`로 되돌린다.
- 씬/프리팹 변경 후에는 반드시 저장한다. 큰 씬 구조 변경 전에는 무엇을 바꿀지 먼저 요약한다.
- `.meta` 파일은 직접 만들거나 수정하지 않는다. 에셋 생성·이동·삭제는 MCP(에디터 API)를 통해 해서 GUID가 깨지지 않게 한다.
- MCP 도구로 할 수 없는 작업(패키지 설치 승인, Project Settings 일부 등)이 있으면 그때만 사용자에게 정확한 경로와 함께 요청한다.
- 매직 넘버 대신 `ScriptableObject` 설정(이동 속도, 점프 높이, 행동 지속시간 등)을 사용한다.
- 로직(행동 검증, 파싱, 시퀀스 기록)은 MonoBehaviour와 분리된 순수 C# 클래스로 작성해 **EditMode 테스트**가 가능하게 한다.

### 테스트 범위 (최소한, 순수 로직만)
깨지면 찾기 어렵고 자주 바뀌지 않는 부분만 EditMode 테스트로 지킨다. 그 외는 플레이 모드 실행 + 콘솔 로그로 검증한다.
- **테스트한다**
  - 한국어 Mock 파서: 예문 → 기대 행동 리스트 (키워드 추가 시 회귀 방지)
  - `ActionValidator`: 행동 개수(4개 이상 → 실행 거부), 허용 ActionType, 대상 존재·공개 여부, 어포던스, 순서 전제 조건
  - 결정론: 같은 입력 시퀀스를 2회 시뮬레이션 → 같은 결과
  - 리셋 범위: 리셋 후 안개 해제 정보는 남고, 적·문·플레이어는 초기 상태인지
- **테스트하지 않는다:** 이동 손맛, 애니메이션, UI 배치, 연출(되감기 포함), 적 반응 타이밍의 체감
- 위 목록 밖의 테스트(인프라, PlayMode 테스트 등)는 사용자가 요청할 때만 추가한다.

---

## 4. 게임 루프 / 상태 머신

```
[Boot] → [Planning] ⇄ [Executing] → (사망) → [Dead] → [Rewind] → [Reset] → [Planning]
                              ↓ (옥상 도달)
                          [Cleared] → [FinalRun 리플레이] → [Result]
```

| 상태 | 설명 |
|---|---|
| Planning | 시간이 아주 느리게 흐름(약 1%) + 시간 지연 연출. 마우스로 목표 구역 선택 + 프롬프트 입력. 입력 완료 시 해석 요청. 이 사이에 죽을 수도 있다 |
| Interpreting | LLM/Mock이 행동 리스트 생성 → 검증. 실패 시 이유를 표시하고 Planning으로 복귀 (런 진행에 영향 없음) |
| Executing | 검증된 행동 1~3개를 순서대로 실행. 적/투사체/물리 진행 |
| Dead | 사망 연출(0.6초) 후 사망 원인 + "다시 시작" 버튼 표시. 버튼/Enter를 누를 때까지 대기 → Rewind |
| Rewind | 이번 런 동안 기록한 플레이어·적·투사체·문의 상태 스냅샷을 역순으로 빠르게 재생하는 **연출 전용** 단계. 입력 없음, 클릭으로 스킵. 끝나면 Reset |
| Reset | 스테이지·적·문·플레이어를 초기 상태로 복원. **발견한 맵 정보(안개 해제)는 유지한다** (월드 상태와 플레이어 지식을 분리해서 리셋 범위를 코드에 명시) |
| Cleared | 옥상 도달. 이번 런의 행동 기록 저장 |
| FinalRun | 기록된 전체 시퀀스를 정지 없이 연속 재생 |

---

## 5. 핵심 시스템 4가지

### 5-1. 프롬프트 → 행동 (최대 3개)

플레이어 입력 예: `"문을 걷어차고 들어가서 앞구르기"`
→ 해석 결과:
```json
{
  "actions": [
    { "type": "KICK_DOOR", "target": "door_1F_A" },
    { "type": "ENTER" },
    { "type": "ROLL", "direction": "forward" }
  ],
  "overLimit": false,
  "notes": ""
}
```
- 행동은 **고수준 단위**다. 예: `WALL_CLIMB_TO_WINDOW`는 내부적으로 여러 애니메이션을 포함하지만 행동 1개로 센다.
- 3개 초과 문장 → **실행 거부**. `overLimit: true`, `actions`에는 인식된 행동을 전부 담아 UI에 "행동이 N개입니다. 3개 이하로 줄여주세요" 같은 수정 안내와 함께 보여준다. 월드 상태는 바뀌지 않으며 런에 기록하지 않는다.
- 해석 불가/모호 → `actions: []` + `notes`에 이유. 게임은 플레이어에게 다시 입력하게 한다.

### 5-2. 마우스로 목표 구역 지정

- 스테이지는 **Zone(구역)** 단위로 나뉜다: 방, 외벽 구간, 창문, 문, 옥상 등.
- Planning 중 커서를 올린 Zone은 외곽선 하이라이트, 클릭하면 목표로 확정.
- **화면에는 방 번호를 표시하지 않는다.** 대신 모든 Zone은 내부 고유 ID(예: `2F-B`, `Window_2F_A`)를 가지고, 플레이어가 선택한 Zone의 ID가 해석기에 전달된다. 좌표만으로는 "어느 방/어느 창문인지" 구분에 한계가 있기 때문.
- **발견하지 않은 Zone은 클릭 불가.**
- 목표 Zone ID는 해석기에 컨텍스트로 전달되며, 행동의 `target` 결정과 경로 판정에 사용된다. 해석기는 좌표가 아니라 ID로 대상을 지칭한다.

### 5-3. 안개(Fog of War) — 미발견 공간 가림

- 플레이어가 들어가지 않은/보지 않은 Zone은 검은 실루엣으로 덮인다 (적, 문, 창문, 구조 전부 숨김).
- Zone은 `Hidden → Revealed` 상태를 가진다. 진입 시, 또는 시야(문 열기·창문 너머 보기 등)로 공개.
- 공개 상태는 **플레이어 지식**이라 죽어도 유지된다. 리셋되는 건 적·문·오브젝트 같은 월드 상태뿐이다.
- 건물 외곽 실루엣 정도는 처음부터 보여도 된다 (어디가 2층인지 감은 오게).

### 5-4. 죽음 = 처음부터, 그리고 기록

- 사망 원인: 적 근접 공격, 총알, 추락, (확장) 폭발 등. 플레이어는 1히트 사망.
- 매 런마다 `RunRecord` = `[ {targetZone, prompt, actions[]} , ... ]`를 기록.
- 클리어한 런의 기록은 저장되며 FinalRun에서 정지 없이 재생된다. 결정론이 깨지면 리플레이가 어긋나므로 반드시 지킬 것.
- 직전 런의 프롬프트 히스토리를 사이드 패널에 보여줘 재사용/수정을 돕는다 (클릭하면 입력창에 복사).

---

## 6. 행동 카탈로그 (Action Catalog)

> 행동 종류는 `ActionType` enum + 각 행동의 `ActionDefinition`(ScriptableObject)로 정의한다.
> 각 행동은 **전제 조건(Precondition)**, **실행(Execute, 코루틴/태스크)**, **소요 시간**, **필요 어포던스(affordance)** 를 가진다.

### 이동
`WALK`, `RUN`, `SPRINT`, `BACKSTEP`, `JUMP`, `DOUBLE_JUMP`, `SLIDE`, `ROLL`, `BACKFLIP`, `CROUCH`

### 이동 응용
`WALL_CLIMB`, `WALL_JUMP`, `GRAB_LEDGE`, `CLIMB_LEDGE`, `VAULT_WINDOW`(열린 창 넘기), `CRASH_WINDOW`(창 깨고 진입), `CLIMB_LADDER`, `EXTERIOR_MOVE`(외벽 이동), `HANG_CEILING`

### 전투
`SLASH`, `STAB`, `KICK`, `SHOVE`, `JUMP_ATTACK`, `ROLL_ATTACK`, `DISARM`(무기 탈취), `THROW`

### 환경 상호작용
`OPEN_DOOR`, `KICK_DOOR`(문 열면서 앞의 적 밀침), `BREAK_DOOR`, `ENTER`, `PRESS_BUTTON`, `PICK_UP`, `THROW_OBJECT`, `USE_EXPLOSIVE`, `WAIT`

### 우선순위 (MVP에서 먼저 구현할 것)
`RUN`, `JUMP`, `ROLL`, `SLIDE`, `OPEN_DOOR`, `KICK_DOOR`, `ENTER`, `SLASH`, `WALL_CLIMB`, `CRASH_WINDOW`, `CLIMB_LADDER`, `WAIT`
나머지는 마일스톤 3 이후 확장.

### 어포던스 (Zone/오브젝트가 허용하는 행동)
각 Zone과 상호작용 오브젝트는 허용 행동 목록을 가진다.
- 예: `Window_2F_A` → `CRASH_WINDOW`, `VAULT_WINDOW` / `ExteriorWall_East` → `WALL_CLIMB`, `EXTERIOR_MOVE`
- 문이 없는 곳에 `OPEN_DOOR` → 검증 실패 ("그곳에는 문이 없다")
- 경로가 없는 Zone으로 `RUN` → 검증 실패 ("갈 수 있는 길이 없다")

---

## 7. AI 해석기 설계 (공급자 미정 → 추상화)

```csharp
public interface IActionInterpreter
{
    Task<InterpretResult> InterpretAsync(InterpretRequest request, CancellationToken ct);
}
```

- `InterpretRequest`: 플레이어 프롬프트, 현재 Zone ID, 플레이어가 클릭한 목표 Zone ID, **공개된** Zone들의 ID·연결 관계· 어포던스 목록, 보이는 적 정보, 허용 ActionType 목록, 최대 행동 수(3)
  - ⚠️ 미발견 정보는 절대 LLM에 넘기지 않는다 (AI가 숨은 적을 알면 게임이 깨짐).
- `InterpretResult`: `actions[]`, `overLimit`, `notes`, `raw`(디버그용 원문)

### 구현체
1. **`MockInterpreter` (필수, 먼저 구현)**
   - 한국어 키워드 기반 규칙 파서. 예: "걷어차" → `KICK_DOOR`, "구르" → `ROLL`, "베" / "공격" → `SLASH`, "벽을 타" → `WALL_CLIMB`, "창문" + "깨" → `CRASH_WINDOW`
   - 문장 내 등장 순서대로 행동 나열, 3개 초과 시 자르지 않고 `overLimit: true`로 반환.
   - 키워드 사전은 ScriptableObject 또는 JSON으로 분리해 쉽게 늘릴 수 있게. → `Assets/_Project/Data/mock_keywords.json`. 키워드 안의 공백은 "조사 + 띄어쓰기 허용", 같은 위치에서는 긴 키워드 우선, `ignore`는 방향어로 오인되는 시간 표현("뒤에" 등).
2. **`LlmInterpreter` (나중에)**
   - 공급자별 어댑터 (`ClaudeProvider`, `OpenAIProvider` 등)를 `ILlmProvider`로 분리.
   - 시스템 프롬프트: 허용 ActionType만 사용, JSON만 출력, 인식한 행동은 개수와 상관없이 전부 나열(자르지 말 것 — 개수 판정은 게임 코드가 함), 불가능하면 빈 배열 + 이유.
   - 응답 JSON 파싱 실패 / 타임아웃(예: 8초) / 네트워크 오류 시 → `MockInterpreter`로 폴백하고 UI에 작게 표시.
   - API 키는 **코드나 저장소에 넣지 않는다.** 환경 변수 또는 `.gitignore`된 로컬 설정 파일(`UserSettings/api_keys.json` 등)에서 읽는다.
   - 같은 (프롬프트+컨텍스트) 요청은 캐시해서 재시도 시 즉시 응답 + 결과 일관성 확보.

### 검증 레이어 (`ActionValidator`)
LLM/Mock 결과 → 검증 → 실행. 검사 항목:
- ActionType이 허용 목록에 있는가
- 개수 ≤ 3
- 대상 오브젝트/Zone이 존재하고 **공개되어** 있는가
- 해당 위치의 어포던스가 그 행동을 허용하는가
- 순서상 전제 조건이 맞는가 (예: `ENTER` 전에 문이 열려 있거나 `KICK_DOOR`/`OPEN_DOOR`가 선행)
실패 시 어떤 행동이 왜 안 되는지 플레이어에게 짧게 보여준다. 실패한 해석은 런에 기록하지 않는다.

---

## 8. 스테이지 1 설계 (데모용, 3층 건물)

```
            [ 옥상 = 클리어 지점 ]
       ┌──────────────┬──────────┐
  3F   │ 3F-A  적 적  │ 3F-B 적  │
       │      (사다리)│  창문    │
       ├──────────────┼──────────┤
  2F   │ 2F-A  적     │ 2F-B 적적│ ← 동쪽 창문
       │   (계단)     │          │
       ├──────────────┼──────────┤
  1F   │ 1F-A  적 적  │ 1F-B 적  │
       │              │          │
       └────[정문]────┴──────────┘ ← 동쪽 외벽(타기 가능)
  PLAYER (건물 밖 시작)
```

- 시작: 건물 밖. 첫 선택지 예시
  - 정문 열고 진입 / 정문 걷어차기 / 정문 부수기
  - 외벽 타고 2F 동쪽 창문 깨고 진입 (우회 루트)
- 최소 2개 이상의 서로 다른 클리어 루트가 존재해야 한다.
  - 구현(M5): 루트 A = 정문 → 1F-A → 1F-B → 사다리 → 2F-B → 실내 문 → 2F-A, 루트 B = 서쪽 외벽 타기 → 2층 창문 깨고 진입 → 2F-A. 이후 공통으로 2F-A → 사다리 → 3F-A → 3F-B → 사다리 → 옥상(클리어). 층 높이 2.6, 고블린 6마리. 문은 걷어차면 바로 너머의 적도 쓰러뜨린다.
  - 해석기 수식어: "N초 동안", "N칸", "벽까지", "잠깐", "두 번"(반복, 행동 수에 셈), "천천히"(걷기), "빨리"(질주). 키워드는 `Data/mock_keywords.json`.
- 적 타입 (첫 빌드): **근접 적 1종만** — 고블린(`Monsters Creatures Fantasy/Goblin`). 가까우면 일정 시간 후 공격.
  - 구현(M4): 같은 공간 구역 + 거리 6 + 높이 차 1 이내면 추격 → 1 이내에서 0.45초 예비 동작 → 사거리 1.3 판정 → 0.6초 회복. 수치는 `ScriptableObjects/Enemies/Goblin.asset`. 예비 동작 중에 구르기(무적)·베기·거리 벌리기로 대응할 수 있다.
  - 플레이어 베기(`SLASH`): 사거리 1.3, 동작 0.43초 중 30% 지점에서 판정, 맞으면 즉사.
  - 원거리 적(예고선 후 발사 → 구르기/슬라이드로 회피)은 첫 빌드가 돌아간 뒤 추가. 에셋에 총·투사체가 없으므로 어떤 몬스터로 할지 그때 정한다. `EnemyBase`는 원거리 적을 붙일 수 있게 설계해 둔다.
- 적은 Executing 중엔 정상 속도, Planning 중엔 아주 느리게, Interpreting 중엔 정지. 적 반응 타이밍은 틱 단위 고정값(결정론).
- 스테이지 데이터는 하드코딩하지 말고 `StageDefinition`(ScriptableObject 또는 JSON)으로 정의: Zone 목록, 연결 관계, 어포던스, 적 스폰, 클리어 Zone.

---

## 9. 화면 / UI

- 메인: 건물 단면 사이드뷰. 카메라는 건물 전체가 보이는 고정 뷰(필요하면 약간의 줌).
- Planning UI
  - 하단 프롬프트 입력창. Planning 중에는 항상 포커스 상태(목표 클릭 후에도 다시 잡힘), **Enter 한 번이면 해석 → 검증 → 바로 실행**
  - 목표 Zone 표시 ("목표: 선택된 구역" 외곽선)
  - 실행 중인 계획 요약: `뒤돌기 → [1] 문 걷어차기 → [2] 진입 → [3] 구르기` (무료 지시는 번호 없음). 해석·검증 실패 시엔 이유만 표시하고 입력은 그대로 둔다
  - 사이드: 이번 런/직전 런 프롬프트 히스토리
- 상단: 시도 횟수(Attempt #N), 현재 런 행동 수
- 사망: 화면 짧게 멈춤 → 가운데에 사망 원인 + "다시 시작 (Enter)" 버튼 → 누르면 되감기 연출(역재생, VHS 느낌 등은 M8에서) → 리셋
- 클리어: "FINAL RUN" 타이틀 후 전체 시퀀스 연속 재생, 끝나면 총 시도 횟수·행동 수·프롬프트 수 표시
- 아트: **픽셀 아트.** 캐릭터·적은 `Assets/ExternalAssets/`의 에셋으로 시작한다.
  - 플레이어: `Hero Knight - Pixel Art` (Idle/Run/Jump/Fall/Roll/Attack1~3/LedgeGrab/WallSlide/Death 등)
  - 적: `Monsters Creatures Fantasy` (Goblin, Skeleton, Mushroom, Flying eye)
  - 외부 에셋 폴더의 파일(스프라이트·애니메이션·스크립트)은 수정하지 않는다. 임포트 설정 변경만 에디터 API로 허용. 필요한 프리팹·애니메이터는 `_Project/` 아래에 새로 만들어 참조한다. 에셋의 데모 스크립트(`HeroKnight.cs` 등)는 게임 로직에 쓰지 않는다.
  - 픽셀 아트 설정(M0에서 적용됨): 모든 스프라이트 Filter Mode = Point, Compression = None, Mipmap 끔, **PPU = 32**. 카메라는 Pixel Perfect Camera(PPU 32, 기준 해상도 640×360 → 화면 20×11.25 유닛).
  - 프레임 크기: 히어로 100×55px, 몬스터 150×150px(캐릭터는 프레임 중앙 부분만 차지). 발 위치 기준 피벗 정렬은 프리팹 만들 때 맞춘다.
  - 히어로 에셋에 전용 애니메이션이 없는 행동은 **기존 동작을 재활용**한다 (예: `SLIDE` → Roll, `WALL_CLIMB`/`CLIMB_LADDER` → WallSlide·LedgeGrab·Jump 조합, `KICK_DOOR` → Attack). 행동 ↔ 애니메이션 매핑은 `ActionDefinition`에 두어 나중에 아트만 교체할 수 있게 한다.
  - 건물·배경은 MVP에서 단색 도형/타일 플레이스홀더로 시작. SpriteRenderer/Animator를 로직과 분리해서 나중에 교체하기 쉽게.

---

## 10. 폴더 구조 (권장)

```
Assets/
  _Project/
    Scripts/
      Core/          # GameStateMachine, RunManager, DeterministicClock
      Actions/       # ActionType, ActionDefinition, ActionExecutor, 각 행동 구현
      Interpretation/# IActionInterpreter, MockInterpreter, LlmInterpreter, ActionValidator, DTO
      Stage/         # StageDefinition, Zone, Affordance, FogOfWar
      Enemies/       # EnemyBase, MeleeEnemy (원거리 적은 나중에)
      Player/        # PlayerController(행동 실행 대상), PlayerHealth
      Replay/        # RunRecord, ReplayPlayer, StateRecorder(틱별 스냅샷), RewindPlayer
      UI/            # PromptInputUI, ZoneHighlighter, HistoryPanel, HUD
    ScriptableObjects/
    Prefabs/
    Scenes/
    Data/            # 키워드 사전, 스테이지 JSON
  Editor/            # 스테이지/씬 생성 툴
  Tests/
    EditMode/        # 파서, 검증기, 결정론, 리셋 범위 테스트 (3장 "테스트 범위" 참고)
```

---

## 11. 개발 마일스톤

각 마일스톤 끝에 **MCP로 플레이 모드를 실행해 직접 검증한 결과**(콘솔 에러 유무, 확인한 동작)와 **사용자가 직접 플레이해 볼 방법**을 정리해 보고한다.

**M0. 프로젝트 셋업**
- Unity MCP 연결 확인 및 사용 가능한 도구 목록 정리
- 폴더 구조, 패키지(Input System, TMP, Newtonsoft), 기본 씬, `.gitignore`(Unity 표준 + API 키 파일)

**M1. 정지-실행 루프 (AI 없이)**
- 게임 상태 머신, 플레이어 + 기본 행동(`RUN`, `JUMP`, `ROLL`, `WAIT`)
- 디버그 UI로 행동 3개를 직접 골라 실행 → 끝나면 정지

**M2. Zone + 마우스 지정 + 안개**
- 테스트 스테이지(StageDefinition + 빌더, MCP로 생성·검증), Zone 하이라이트/클릭, Fog 공개 로직

**M3. 프롬프트 + MockInterpreter + 검증기**
- 입력창, 한국어 키워드 파서, ActionValidator, 실패 메시지, 해석 미리보기
- EditMode 테스트: "문을 걷어차고 들어가서 앞구르기 후 공격" → 4개로 인식되어 실행 거부 + 수정 안내되는지 등

**M4. 적 + 죽음/리셋**
- 근접 적 1종(고블린), 1히트 사망, 다시 시작 버튼 + 되감기 연출 + 리셋, 시도 횟수
- EditMode 테스트: 결정론(같은 입력 2회 → 같은 결과), 리셋 범위(안개 해제 유지 / 월드 상태 초기화)

**M5. 스테이지 1 완성 + 환경 행동**
- 문/창문/외벽/사다리 행동, 3층 + 옥상 클리어, 2개 이상 루트

**M6. 기록 + FINAL RUN 리플레이**
- 구현: 클리어 → "CLEAR" 1.2초 → FINAL RUN(월드만 초기화, 기록된 프롬프트를 기록된 틱에 다시 넣어 정상 속도로 연속 재생, 프롬프트 자막, 클릭으로 건너뛰기) → 결과(총 시도·프롬프트·행동·시간, 처음부터 다시). 원래 클리어 틱과 다르면 "어긋났다" 표시. `RunReplayer`는 EditMode 테스트로 비트 단위 재현을 검증한다.
- **주의:** 틱 밖(상태 전이 코드 등)에서 속도·위치 같은 월드 상태를 바꾸면 FINAL RUN이 어긋난다. 월드 상태는 틱 안에서만 바꾼다.
- RunRecord 저장, 정지 없는 연속 재생, 결과 화면

**M7. LLM 연동**
- 구현: `LlmInterpreter`(캐시 + 실패 시 Mock 폴백) → `ClaudeProvider`(구조화 출력 `output_config.format` json_schema, 시스템 프롬프트 `cache_control`, 서버 측 `fallbacks: "default"`, effort `low`, 타임아웃 8초). 시스템 프롬프트·스키마는 허용 행동 목록만으로 정해지는 고정 텍스트(캐싱), 상황(공개된 구역·목표·명령)은 user 메시지.
- API 키: 환경 변수 `ANTHROPIC_API_KEY` 또는 `UserSettings/api_keys.json` (`{"anthropic": "sk-ant-..."}`, git 무시). 키가 없으면 키워드 해석기로 동작하고 상태 줄에 표시.
- FINAL RUN은 해석된 행동을 기록하므로 LLM 응답이 매번 달라도 재현에 영향이 없다.
- Ollama(`OllamaProvider`): `/api/chat`에 `format`=JSON 스키마, `think:false`, `temperature 0`, `keep_alive 30m`. 게임 시작 시 모델을 미리 올린다(첫 로딩 수십 초). 응답 3~5초, 타임아웃 20초. Ollama가 꺼져 있으면 키워드 해석기로 대체.
- `ILlmProvider` 어댑터, 시스템 프롬프트, JSON 스키마 검증, 폴백, 캐시

**M8. 폴리싱**
- 구현: 히트스톱(적 처치 0.07초·문 차기 0.05초, 실제 시간으로 틱 진행만 잠시 멈춤 → 결정론·FINAL RUN 영향 없음), 화면 흔들림(정수 픽셀 단위, `CameraShake`), 코드로 만든 레트로 효과음(`ProceduralSfx`, `GameFeedback`의 Overrides에 실제 사운드를 넣으면 교체), 튜토리얼(입력칸 예시 프롬프트 순환, 첫 시도 목표 안내). 캐릭터 아트 교체는 미정.
- 연출(히트스톱, 슬로모션, 화면 흔들림), 사운드, 아트 교체, 튜토리얼 프롬프트 예시

---

## 12. 코딩 규칙

- C# 네이밍: 클래스/메서드 PascalCase, private 필드 `_camelCase`, `[SerializeField] private` 선호
- `Update`에 게임 로직 몰아넣지 말 것. 상태 머신 + 행동 실행기 중심.
- 행동 실행은 시간 기반(코루틴 또는 고정 틱). 리플레이 정확도를 위해 **고정 틱(FixedUpdate 기반 시뮬레이션) 권장**.
- 새 ActionType을 추가하려면: enum 추가 → ActionDefinition 에셋 → 실행 클래스 → Mock 키워드 → LLM 시스템 프롬프트 허용 목록 → 테스트. 이 체크리스트를 지킬 것.
- 큰 변경 전에는 계획을 먼저 제시하고, 작업 후 변경 파일 목록과 확인 방법을 요약한다.

---

## 13. 아직 정하지 않은 것 (작업 중 임의로 확정하지 말고 질문할 것)

- 캐릭터 설정, 게임 정식 제목 (아트 스타일은 픽셀 아트로 확정)
- 스테이지 2 이상 확장 여부
