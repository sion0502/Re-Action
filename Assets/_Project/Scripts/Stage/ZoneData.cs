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
        [Tooltip("외벽: 건물이 있는 쪽 (+1 오른쪽, -1 왼쪽). 이쪽 면에 붙어서 오른다")]
        public int WallSide = 1;

        public float Area => Bounds.width * Bounds.height;
    }

    public enum EnemyKind
    {
        Goblin,
    }

    /// <summary>적 배치. 위치는 발 기준, Facing은 -1(왼쪽) 또는 1(오른쪽).</summary>
    [Serializable]
    public sealed class EnemySpawn
    {
        public EnemyKind Kind;
        public Vector2 Position;
        public int Facing = -1;
    }

    /// <summary>스테이지 지형 블록. Solid가 아니면 배경 장식이다.</summary>
    [Serializable]
    public sealed class BlockData
    {
        public string Name;
        public Rect Bounds;
        public bool Solid = true;
        [Tooltip("아래에서 위로는 통과하고 위에서는 밟을 수 있는 발판 (사다리 구멍 덮개)")]
        public bool OneWay;
        public Color Color = Color.gray;
        public int SortingOrder;
    }
}
