using System.Collections.Generic;
using ReAction.Actions;
using ReAction.Stage;
using UnityEngine;

namespace ReAction.Editor
{
    /// <summary>
    /// M2 테스트 스테이지 데이터: 2층 건물 + 옥상. 실제 스테이지 1(3층)은 M5에서 따로 만든다.
    /// 화면은 20 x 11.25 유닛, 바닥 윗면 y = -4. 건물은 x -4 ~ 8.
    /// </summary>
    public static class TestStageData
    {
        private static readonly Color Outer = new Color(0.35f, 0.33f, 0.4f);
        private static readonly Color Wall = new Color(0.28f, 0.26f, 0.33f);
        private static readonly Color Backdrop = new Color(0.14f, 0.13f, 0.17f);
        private static readonly Color Crate = new Color(0.5f, 0.42f, 0.32f);

        public static void Fill(StageDefinition definition)
        {
            var zones = new List<ZoneData>
            {
                Zone("EXT_West", ZoneKind.Exterior, 0, Box(-9f, -4f, -4f, 5.6f), revealed: true,
                    connections: new[] { "Door_1F_West" }),
                Zone("Door_1F_West", ZoneKind.Door, 1, Box(-4.25f, -4f, -3.5f, -2.5f), revealed: true,
                    connections: new[] { "EXT_West", "1F-A" },
                    affordances: new[] { ActionType.OpenDoor, ActionType.KickDoor, ActionType.BreakDoor, ActionType.Enter }),
                Zone("1F-A", ZoneKind.Room, 1, Box(-3.75f, -4f, 2f, -1f),
                    connections: new[] { "Door_1F_West", "1F-B" }),
                Zone("1F-B", ZoneKind.Room, 1, Box(2f, -4f, 7.75f, -1f),
                    connections: new[] { "1F-A" }),
                Zone("2F-A", ZoneKind.Room, 2, Box(-3.75f, -0.75f, 7.75f, 2.25f),
                    connections: new[] { "Window_2F_East", "ROOF" }),
                Zone("Window_2F_East", ZoneKind.Window, 2, Box(7.5f, 0f, 8.25f, 1.5f), revealed: true,
                    connections: new[] { "2F-A", "ExteriorWall_East" },
                    affordances: new[] { ActionType.CrashWindow, ActionType.VaultWindow }),
                Zone("ExteriorWall_East", ZoneKind.ExteriorWall, 0, Box(8f, -4f, 9f, 2.5f), revealed: true,
                    connections: new[] { "Window_2F_East", "ROOF" },
                    affordances: new[] { ActionType.WallClimb, ActionType.ExteriorMove }),
                Zone("ROOF", ZoneKind.Roof, 3, Box(-4f, 2.5f, 8f, 5f), revealed: true,
                    connections: new[] { "2F-A", "ExteriorWall_East" }),
            };

            var blocks = new List<BlockData>
            {
                // 배경 (충돌 없음)
                Block("Building_Backdrop", Box(-3.75f, -4f, 7.75f, 2.25f), Backdrop, solid: false, order: -10),

                // 바깥 지형
                Block("Ground", Box(-10f, -5f, 10f, -4f), Outer),
                Block("World_Left", Box(-10f, -5.625f, -9f, 5.625f), Outer),
                Block("World_Right", Box(9f, -5.625f, 10f, 5.625f), Outer),

                // 건물 외벽 (서쪽 1층은 정문 자리로 비워 둠)
                Block("Wall_West_Upper", Box(-4f, -2.5f, -3.75f, 2.25f), Wall),
                Block("Wall_East", Box(7.75f, -4f, 8f, 2.25f), Wall),

                // 층 바닥 / 지붕
                Block("Slab_2F", Box(-4f, -1f, 8f, -0.75f), Wall),
                Block("Slab_Roof", Box(-4f, 2.25f, 8f, 2.5f), Wall),

                // 1층 칸막이 (아래쪽은 통로)
                Block("Partition_1F", Box(2f, -2.5f, 2.25f, -1f), Wall),

                // 방 안 장애물 (안개에 가려져 있어야 함)
                Block("Crate_1F_A", Box(-0.5f, -4f, 0.5f, -3f), Crate),
                Block("Crate_1F_B", Box(4.5f, -4f, 5.5f, -3f), Crate),
            };

            var enemies = new List<EnemySpawn>
            {
                // 1F-A: 상자 너머에서 기다린다. 상자를 넘으면 바로 마주친다.
                new EnemySpawn { Kind = EnemyKind.Goblin, Position = new Vector2(1.3f, -4f), Facing = -1 },
                // 1F-B 안쪽
                new EnemySpawn { Kind = EnemyKind.Goblin, Position = new Vector2(6.8f, -4f), Facing = -1 },
            };

            definition.EditorSet(zones, blocks, enemies, playerStart: new Vector2(-7f, -4f), clearZoneId: "ROOF");
        }

        private static Rect Box(float xMin, float yMin, float xMax, float yMax) =>
            Rect.MinMaxRect(xMin, yMin, xMax, yMax);

        private static ZoneData Zone(string id, ZoneKind kind, int floor, Rect bounds, bool revealed = false,
            string[] connections = null, ActionType[] affordances = null) =>
            new ZoneData
            {
                Id = id,
                Kind = kind,
                Floor = floor,
                Bounds = bounds,
                RevealedAtStart = revealed,
                Connections = connections ?? new string[0],
                Affordances = affordances ?? new ActionType[0],
            };

        private static BlockData Block(string name, Rect bounds, Color color, bool solid = true, int order = 0) =>
            new BlockData { Name = name, Bounds = bounds, Color = color, Solid = solid, SortingOrder = order };
    }
}
