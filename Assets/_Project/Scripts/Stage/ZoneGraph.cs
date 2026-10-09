using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReAction.Stage
{
    /// <summary>
    /// 구역 조회와 연결 관계. 위치로 구역을 찾을 때는 겹친 구역 중 가장 작은 것을 고른다
    /// (방 안의 문·창문이 방보다 우선).
    /// </summary>
    public sealed class ZoneGraph
    {
        private readonly Dictionary<string, ZoneData> _byId = new Dictionary<string, ZoneData>();
        private readonly List<ZoneData> _zones = new List<ZoneData>();

        public ZoneGraph(IEnumerable<ZoneData> zones)
        {
            foreach (var zone in zones)
            {
                if (zone == null || string.IsNullOrEmpty(zone.Id))
                    throw new ArgumentException("Zone without id");
                if (_byId.ContainsKey(zone.Id))
                    throw new ArgumentException($"Duplicate zone id: {zone.Id}");
                _byId.Add(zone.Id, zone);
                _zones.Add(zone);
            }
        }

        public IReadOnlyList<ZoneData> Zones => _zones;

        public ZoneData Get(string id) =>
            id != null && _byId.TryGetValue(id, out var zone) ? zone : null;

        /// <summary>point를 포함하는 가장 작은 구역. filter가 있으면 통과한 구역만 본다.</summary>
        public ZoneData FindAt(Vector2 point, Func<ZoneData, bool> filter = null)
        {
            ZoneData best = null;
            foreach (var zone in _zones)
            {
                if (!zone.Bounds.Contains(point)) continue;
                if (filter != null && !filter(zone)) continue;
                if (best == null || zone.Area < best.Area) best = zone;
            }
            return best;
        }

        /// <summary>양방향 연결 여부. 한쪽에만 적혀 있어도 연결된 것으로 본다.</summary>
        public bool AreConnected(string a, string b)
        {
            var za = Get(a);
            var zb = Get(b);
            if (za == null || zb == null) return false;
            return Array.IndexOf(za.Connections, b) >= 0 || Array.IndexOf(zb.Connections, a) >= 0;
        }
    }
}
