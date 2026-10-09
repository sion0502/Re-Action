using System;
using ReAction.Actions;
using UnityEngine;

namespace ReAction.Stage
{
    public enum ZoneKind
    {
        Room,
        Exterior,
        ExteriorWall,
        Roof,
        Door,
        Window,
        Ladder,
    }

    public static class ZoneKindExtensions
    {
        /// <summary>플레이어가 "안에 들어가 있을 수 있는" 공간인지. 문·창문 같은 지점형 구역은 아니다.</summary>
        public static bool IsSpace(this ZoneKind kind) =>
            kind == ZoneKind.Room || kind == ZoneKind.Exterior || kind == ZoneKind.Roof;
    }

    /// <summary>
    /// 구역 하나의 정의. Id는 화면에 표시하지 않고 해석기·검증기에만 전달된다.
    /// </summary>
    [Serializable]
    public sealed class ZoneData
    {
        public string Id;
        public ZoneKind Kind;
        public int Floor;
        [Tooltip("월드 좌표 사각형 (x, y = 왼쪽 아래)")]
        public Rect Bounds;
        public string[] Connections = Array.Empty<string>();
        public ActionType[] Affordances = Array.Empty<ActionType>();
        [Tooltip("처음부터 공개된 구역 (건물 바깥, 정문 등)")]
        public bool RevealedAtStart;

        public float Area => Bounds.width * Bounds.height;
    }

    /// <summary>스테이지 지형 블록. Solid가 아니면 배경 장식이다.</summary>
    [Serializable]
    public sealed class BlockData
    {
        public string Name;
        public Rect Bounds;
        public bool Solid = true;
        public Color Color = Color.gray;
        public int SortingOrder;
    }
}
