using System.Collections.Generic;
using ReAction.Core;
using ReAction.Player;
using UnityEngine;

namespace ReAction.Stage
{
    /// <summary>
    /// 스테이지 런타임: 구역 그래프, 발견 상태(안개), 플레이어가 있는 구역 추적.
    /// 안개는 공개되지 않은 구역 위에 덮는 어두운 사각형으로 표시한다 (런타임 생성).
    /// </summary>
    public sealed class StageController : MonoBehaviour
    {
        private const int FogSortingOrder = 100;

        [SerializeField] private StageDefinition _definition;
        [SerializeField] private GameController _game;
        [SerializeField] private PlayerMotor _player;
        [SerializeField] private Sprite _fogSprite;
        [SerializeField] private Color _fogColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        [SerializeField] private float _fogFadeSeconds = 0.25f;

        [Tooltip("플레이어 위치에서 이 만큼 올린 점으로 현재 구역을 판정 (몸 중앙)")]
        [SerializeField] private Vector2 _playerProbeOffset = new Vector2(0f, 0.6f);

        private readonly Dictionary<string, SpriteRenderer> _fog = new Dictionary<string, SpriteRenderer>();
        private readonly List<SpriteRenderer> _fading = new List<SpriteRenderer>();

        public StageDefinition Definition => _definition;
        public ZoneGraph Graph { get; private set; }
        public ZoneKnowledge Knowledge { get; private set; }
        public string CurrentZoneId { get; private set; }

        private void Awake()
        {
            Graph = new ZoneGraph(_definition.Zones);
            Knowledge = new ZoneKnowledge(Graph);
            Knowledge.Revealed += OnRevealed;
            CreateFog();
        }

        private void OnEnable() => _game.Ticked += OnTicked;
        private void OnDisable() => _game.Ticked -= OnTicked;

        private void Start() => UpdatePlayerZone();

        private void OnTicked(int tick) => UpdatePlayerZone();

        private void UpdatePlayerZone()
        {
            var zone = Graph.FindAt(_player.Position + _playerProbeOffset, z => z.Kind.IsSpace());
            string id = zone?.Id;
            if (id == CurrentZoneId) return;

            CurrentZoneId = id;
            PzLog.Info($"Player zone -> {id ?? "(none)"}");
            if (id != null) Knowledge.Reveal(id);
        }

        private void OnRevealed(string zoneId)
        {
            PzLog.Info($"Revealed {zoneId}");
            if (_fog.TryGetValue(zoneId, out var fog)) _fading.Add(fog);
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
                if (Knowledge.IsRevealed(zone.Id)) continue;

                var go = new GameObject($"Fog_{zone.Id}");
                go.transform.SetParent(root, false);
                go.transform.position = zone.Bounds.center;
                go.transform.localScale = new Vector3(zone.Bounds.width, zone.Bounds.height, 1f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _fogSprite;
                renderer.color = _fogColor;
                renderer.sortingOrder = FogSortingOrder;
                _fog.Add(zone.Id, renderer);
            }
        }
    }
}
