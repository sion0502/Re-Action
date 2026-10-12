using System.Collections.Generic;
using System.Linq;
using ReAction.Core;
using ReAction.Player;
using UnityEngine;

namespace ReAction.Stage
{
    /// <summary>
    /// 스테이지 런타임: 구역 그래프, 발견 상태(안개), 플레이어가 있는 구역 추적, 문·창문 조회, 클리어 판정.
    /// 안개는 공개되지 않은 공간(방) 위에 덮는 어두운 사각형으로 표시한다 (런타임 생성).
    /// </summary>
    public sealed class StageController : MonoBehaviour, IStageWorld
    {
        private const int FogSortingOrder = 100;
        // 방 사이 벽·문틀까지 덮도록 안개를 조금 크게 그린다
        private const float FogPadding = 0.15f;

        [SerializeField] private StageDefinition _definition;
        [SerializeField] private GameController _game;
        [SerializeField] private PlayerMotor _player;
        [SerializeField] private Sprite _fogSprite;
        [SerializeField] private Color _fogColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        [SerializeField] private float _fogFadeSeconds = 0.25f;
        [Tooltip("가려진 방 가운데에 띄우는 물음표 (없으면 안 띄운다)")]
        [SerializeField] private Sprite _fogMarkSprite;
        [SerializeField] private Color _fogMarkColor = Color.white;

        [Tooltip("플레이어 위치에서 이 만큼 올린 점으로 현재 구역을 판정 (몸 중앙)")]
        [SerializeField] private Vector2 _playerProbeOffset = new Vector2(0f, 0.6f);

        private readonly Dictionary<string, SpriteRenderer> _fog = new Dictionary<string, SpriteRenderer>();
        private readonly List<SpriteRenderer> _fading = new List<SpriteRenderer>();
        // 안개 → 그 위 물음표 (안개와 같이 사라진다)
        private readonly Dictionary<SpriteRenderer, SpriteRenderer> _fogMarks = new Dictionary<SpriteRenderer, SpriteRenderer>();
        private readonly Dictionary<string, Door> _doors = new Dictionary<string, Door>();
        private readonly Dictionary<string, BreakableWindow> _windows = new Dictionary<string, BreakableWindow>();

        public StageDefinition Definition => _definition;
        public ZoneGraph Graph { get; private set; }
        public ZoneKnowledge Knowledge { get; private set; }
        public string CurrentZoneId { get; private set; }

        /// <summary>문·창문 (월드 리셋 대상). GameController가 등록한다.</summary>
        public IEnumerable<IResettable> Resettables => _doors.Values.Cast<IResettable>().Concat(_windows.Values);

        private void Awake()
        {
            Graph = new ZoneGraph(_definition.Zones);
            Knowledge = new ZoneKnowledge(Graph);
            Knowledge.Revealed += OnRevealed;

            foreach (var door in GetComponentsInChildren<Door>(true))
            {
                door.Initialize();
                _doors[door.ZoneId] = door;
                door.Opened += d => RevealConnectedSpaces(d.ZoneId);
            }
            foreach (var window in GetComponentsInChildren<BreakableWindow>(true))
            {
                window.Initialize();
                _windows[window.ZoneId] = window;
                window.Broken += w => RevealConnectedSpaces(w.ZoneId);
            }

            CreateFog();
        }

        private void OnEnable()
        {
            _game.Ticked += OnTicked;
            _game.WorldReset += UpdatePlayerZone;
        }

        private void OnDisable()
        {
            _game.Ticked -= OnTicked;
            _game.WorldReset -= UpdatePlayerZone;
        }

        private void Start()
        {
            UpdatePlayerZone();
            // 메인 화면에서는 배경으로 건물 전체가 보이게 한다.
            if (_game != null && _game.StartsWithTitle) HideAllFog();
        }

        public ZoneData GetZone(string zoneId) => Graph.Get(zoneId);
        public Door GetDoor(string zoneId) => zoneId != null && _doors.TryGetValue(zoneId, out var d) ? d : null;
        public BreakableWindow GetWindow(string zoneId) => zoneId != null && _windows.TryGetValue(zoneId, out var w) ? w : null;

        public bool IsEntryOpen(string zoneId)
        {
            var door = GetDoor(zoneId);
            if (door != null) return door.IsPassable;
            var window = GetWindow(zoneId);
            if (window != null) return window.IsBroken;
            return true;
        }

        private void OnTicked(int tick)
        {
            UpdatePlayerZone();
            if (CurrentZoneId != null && CurrentZoneId == _definition.ClearZoneId) _game.NotifyCleared();
        }

        private void UpdatePlayerZone()
        {
            var zone = Graph.FindAt(_player.Position + _playerProbeOffset, z => z.Kind.IsSpace());
            string id = zone?.Id;
            if (id == CurrentZoneId) return;

            CurrentZoneId = id;
            PzLog.Info($"Player zone -> {id ?? "(none)"}");
            if (id != null) Knowledge.Reveal(id);
        }

        /// <summary>문을 열거나 창문을 깨면 그 너머 공간이 보인다.</summary>
        private void RevealConnectedSpaces(string entryZoneId)
        {
            var entry = Graph.Get(entryZoneId);
            if (entry == null) return;
            foreach (var id in entry.Connections)
            {
                var zone = Graph.Get(id);
                if (zone != null && zone.Kind.IsSpace()) Knowledge.Reveal(id);
            }
        }

        private void OnRevealed(string zoneId)
        {
            PzLog.Info($"Revealed {zoneId}");
            if (_fog.TryGetValue(zoneId, out var fog)) _fading.Add(fog);

            // 방이 보이면 그 방에 붙은 사다리·안쪽 문도 알게 된다.
            var revealed = Graph.Get(zoneId);
            if (revealed == null || !revealed.Kind.IsSpace()) return;
            foreach (var zone in Graph.Zones)
            {
                if (zone.Kind.IsSpace() || Knowledge.IsRevealed(zone.Id)) continue;
                if (System.Array.IndexOf(zone.Connections, zoneId) >= 0) Knowledge.Reveal(zone.Id);
            }
        }

        /// <summary>메인 화면 배경용: 안개를 화면에서만 모두 걷는다. 플레이어 지식(발견 정보)은 바꾸지 않는다.</summary>
        public void HideAllFog()
        {
            foreach (var fog in _fog.Values) fog.gameObject.SetActive(false);
            _fading.Clear();
        }

        private void Update()
        {
            // 안개 걷힘은 연출이라 실제 시간으로 진행한다 (시뮬레이션과 무관).
            for (int i = _fading.Count - 1; i >= 0; i--)
            {
                var fog = _fading[i];
                var c = fog.color;
                c.a = _fogFadeSeconds > 0f ? Mathf.MoveTowards(c.a, 0f, Time.deltaTime / _fogFadeSeconds) : 0f;
                fog.color = c;
                if (_fogMarks.TryGetValue(fog, out var mark))
                {
                    var mc = mark.color;
                    mc.a = _fogMarkColor.a * c.a;
                    mark.color = mc;
                }
                if (c.a > 0f) continue;
                fog.gameObject.SetActive(false);
                _fading.RemoveAt(i);
            }
        }

        private void CreateFog()
        {
            var root = new GameObject("Fog").transform;
            root.SetParent(transform, false);

            foreach (var zone in Graph.Zones)
            {
                // 안개는 공간(방)에만 덮는다. 사다리·문 같은 지점은 방 안개에 함께 가려진다.
                if (!zone.Kind.IsSpace() || Knowledge.IsRevealed(zone.Id)) continue;

                var go = new GameObject($"Fog_{zone.Id}");
                go.transform.SetParent(root, false);
                go.transform.position = zone.Bounds.center;
                go.transform.localScale = new Vector3(zone.Bounds.width + FogPadding * 2f, zone.Bounds.height + FogPadding * 2f, 1f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _fogSprite;
                renderer.color = _fogColor;
                renderer.sortingOrder = FogSortingOrder;
                _fog.Add(zone.Id, renderer);
                if (_fogMarkSprite != null) _fogMarks.Add(renderer, CreateFogMark(go.transform, zone));
            }
        }

        /// <summary>
        /// 가려진 방 가운데 물음표. 안개(방 크기로 늘린 사각형)의 자식이라 배율을 되돌려 원래 도트 크기로 그리고,
        /// 위치는 도트 격자(1/32)에 맞춰 픽셀이 뭉개지지 않게 한다.
        /// </summary>
        private SpriteRenderer CreateFogMark(Transform fog, ZoneData zone)
        {
            const float pixelsPerUnit = 32f;
            var go = new GameObject("Mark");
            go.transform.SetParent(fog, false);
            var center = zone.Bounds.center;
            go.transform.position = new Vector3(Mathf.Round(center.x * pixelsPerUnit) / pixelsPerUnit,
                Mathf.Round(center.y * pixelsPerUnit) / pixelsPerUnit, fog.position.z);
            var scale = fog.localScale;
            go.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _fogMarkSprite;
            renderer.color = _fogMarkColor;
            renderer.sortingOrder = FogSortingOrder + 1;
            return renderer;
        }
    }
}
