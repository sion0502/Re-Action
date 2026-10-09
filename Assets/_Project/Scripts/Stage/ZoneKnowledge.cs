using System;
using System.Collections.Generic;

namespace ReAction.Stage
{
    /// <summary>
    /// 플레이어가 발견한 구역 목록 (안개 해제 상태). 플레이어 지식이므로 죽어도 유지되고,
    /// 월드 상태 리셋과 별개로 관리한다. 새 게임을 시작할 때만 ResetToInitial을 부른다.
    /// </summary>
    public sealed class ZoneKnowledge
    {
        private readonly ZoneGraph _graph;
        private readonly HashSet<string> _revealed = new HashSet<string>();

        public ZoneKnowledge(ZoneGraph graph)
        {
            _graph = graph;
            ResetToInitial();
        }

        public event Action<string> Revealed;

        public IReadOnlyCollection<string> RevealedIds => _revealed;

        public bool IsRevealed(string zoneId) => zoneId != null && _revealed.Contains(zoneId);

        /// <summary>새로 공개됐으면 true.</summary>
        public bool Reveal(string zoneId)
        {
            if (_graph.Get(zoneId) == null || !_revealed.Add(zoneId)) return false;
            Revealed?.Invoke(zoneId);
            return true;
        }

        public void ResetToInitial()
        {
            _revealed.Clear();
            foreach (var zone in _graph.Zones)
            {
                if (zone.RevealedAtStart) _revealed.Add(zone.Id);
            }
        }
    }
}
