using System.Collections.Generic;
using UnityEngine;

namespace ReAction.Stage
{
    /// <summary>
    /// 스테이지 하나의 데이터: 구역, 지형, 시작 위치, 클리어 구역.
    /// 씬은 이 데이터로부터 빌더가 만든다 (하드코딩 금지).
    /// </summary>
    [CreateAssetMenu(menuName = "ReAction/Stage Definition", fileName = "Stage")]
    public sealed class StageDefinition : ScriptableObject
    {
        [SerializeField] private List<ZoneData> _zones = new List<ZoneData>();
        [SerializeField] private List<BlockData> _blocks = new List<BlockData>();
        [SerializeField] private List<EnemySpawn> _enemies = new List<EnemySpawn>();
        [SerializeField] private Vector2 _playerStart;
        [SerializeField] private string _clearZoneId;

        public IReadOnlyList<ZoneData> Zones => _zones;
        public IReadOnlyList<BlockData> Blocks => _blocks;
        public IReadOnlyList<EnemySpawn> Enemies => _enemies;
        public Vector2 PlayerStart => _playerStart;
        public string ClearZoneId => _clearZoneId;

#if UNITY_EDITOR
        public void EditorSet(List<ZoneData> zones, List<BlockData> blocks, List<EnemySpawn> enemies,
            Vector2 playerStart, string clearZoneId)
        {
            _zones = zones;
            _blocks = blocks;
            _enemies = enemies;
            _playerStart = playerStart;
            _clearZoneId = clearZoneId;
        }
#endif
    }
}
