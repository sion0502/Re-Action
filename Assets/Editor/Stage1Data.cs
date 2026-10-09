using System.Collections.Generic;
using ReAction.Actions;
using ReAction.Stage;
using UnityEngine;

namespace ReAction.Editor
{
    /// <summary>
    /// 스테이지 1: 3층 건물 + 옥상(클리어). 화면 20 x 11.25 유닛 안에 전부 들어간다.
    ///
    ///   y  5.6 ┌──────────────── ROOF (클리어) ─────────────┐
    ///      3.55├────────────────────────────┬[L3]──────────┤
    ///   3F     │ 3F-A  고블린               │ 3F-B 고블린    │
    ///      0.7 ├──[L2]──────────────────────┴───────────────┤
    ///   2F  [창]│ 2F-A  고블린           [문] 2F-B 고블린    │
    ///     -2.15├────────────────────────────────────[L1]────┤
    ///   1F  [문]│ 1F-A  고블린              │ 1F-B 고블린    │
    ///     -5.0 └───────────────────────────┴────────────────┘
    ///  서쪽 외벽(2층 창문 아래까지 타고 오를 수 있음)
    ///
    /// 루트 A: 정문 → 1F-A → 1F-B → 사다리 L1 → 2F-B → 실내 문 → 2F-A → L2 → 3F-A → 3F-B → L3 → 옥상
    /// 루트 B: 서쪽 외벽 타기 → 2층 창문 깨고 진입 → 2F-A → L2 → 3F-A → 3F-B → L3 → 옥상
    /// </summary>
    public static class Stage1Data
    {
        private static readonly Color Outer = new Color(0.35f, 0.33f, 0.4f);
        private static readonly Color Wall = new Color(0.28f, 0.26f, 0.33f);
        private static readonly Color Backdrop = new Color(0.14f, 0.13f, 0.17f);
        private static readonly Color Ladder = new Color(0.55f, 0.42f, 0.25f, 0.85f);
        private static readonly Color Hatch = new Color(0.45f, 0.36f, 0.24f);

        // 층 높이 (바닥 윗면)
        private const float F1 = -5.0f;
        private const float F2 = -2.15f;
        private const float F3 = 0.7f;
        private const float RoofFloor = 3.55f;
        private const float SlabThickness = 0.25f;

        /// <summary>카메라 중심 y. 하단 프롬프트 바(약 1유닛)가 바닥을 가리지 않도록 1유닛 내려 둔다.</summary>
        public const float CameraY = -1f;

        // 건물 가로 범위와 칸막이
        private const float West = -5.5f;   // 서쪽 외벽 바깥면
        private const float WestIn = -5.25f;
        private const float East = 9.0f;    // 동쪽 외벽 바깥면
        private const float EastIn = 8.75f;
        private const float PartL = 1.75f;
        private const float PartR = 2.0f;

        private const float DoorHeight = 1.5f;
        private const float WindowBottom = -1.95f;
        private const float WindowTop = -0.45f;

        public static void Fill(StageDefinition definition)
        {
            var zones = new List<ZoneData>
            {
                Zone("EXT_West", ZoneKind.Exterior, 0, Box(-9.75f, F1, West, CameraY + 5.625f), revealed: true,
                    connections: new[] { "Door_1F_West", "ExteriorWall_West" }),
                Zone("Door_1F_West", ZoneKind.Door, 1, Box(West - 0.25f, F1, WestIn + 0.25f, F1 + DoorHeight), revealed: true,
                    connections: new[] { "EXT_West", "1F-A" },
                    affordances: new[] { ActionType.OpenDoor, ActionType.KickDoor, ActionType.BreakDoor, ActionType.Enter }),
                Zone("ExteriorWall_West", ZoneKind.ExteriorWall, 0, Box(West - 0.8f, F1, West, WindowBottom + 0.05f), revealed: true,
                    connections: new[] { "EXT_West", "Window_2F_West" },
                    affordances: new[] { ActionType.WallClimb }, wallSide: 1),
                Zone("Window_2F_West", ZoneKind.Window, 2, Box(West - 0.25f, WindowBottom, WestIn + 0.25f, WindowTop), revealed: true,
                    connections: new[] { "ExteriorWall_West", "2F-A" },
                    affordances: new[] { ActionType.CrashWindow, ActionType.VaultWindow, ActionType.Enter }),

                Zone("1F-A", ZoneKind.Room, 1, Box(WestIn, F1, PartL, F2 - SlabThickness),
                    connections: new[] { "Door_1F_West", "1F-B" }),
                Zone("1F-B", ZoneKind.Room, 1, Box(PartR, F1, EastIn, F2 - SlabThickness),
                    connections: new[] { "1F-A", "Ladder_1F_B" }),
                Zone("Ladder_1F_B", ZoneKind.Ladder, 1, Box(7.0f, F1, 8.0f, F2),
                    connections: new[] { "1F-B", "2F-B" },
                    affordances: new[] { ActionType.ClimbLadder }),

                Zone("2F-A", ZoneKind.Room, 2, Box(WestIn, F2, PartL, F3 - SlabThickness),
                    connections: new[] { "Window_2F_West", "Door_2F_Mid", "Ladder_2F_A" }),
                Zone("Door_2F_Mid", ZoneKind.Door, 2, Box(PartL - 0.25f, F2, PartR + 0.25f, F2 + DoorHeight),
                    connections: new[] { "2F-A", "2F-B" },
                    affordances: new[] { ActionType.OpenDoor, ActionType.KickDoor, ActionType.BreakDoor, ActionType.Enter }),
                Zone("2F-B", ZoneKind.Room, 2, Box(PartR, F2, EastIn, F3 - SlabThickness),
                    connections: new[] { "Ladder_1F_B", "Door_2F_Mid" }),
                Zone("Ladder_2F_A", ZoneKind.Ladder, 2, Box(-4.5f, F2, -3.5f, F3),
                    connections: new[] { "2F-A", "3F-A" },
                    affordances: new[] { ActionType.ClimbLadder }),

                Zone("3F-A", ZoneKind.Room, 3, Box(WestIn, F3, PartL, RoofFloor - SlabThickness),
                    connections: new[] { "Ladder_2F_A", "3F-B" }),
                Zone("3F-B", ZoneKind.Room, 3, Box(PartR, F3, EastIn, RoofFloor - SlabThickness),
                    connections: new[] { "3F-A", "Ladder_3F_B" }),
                Zone("Ladder_3F_B", ZoneKind.Ladder, 3, Box(7.0f, F3, 8.0f, RoofFloor),
                    connections: new[] { "3F-B", "ROOF" },
                    affordances: new[] { ActionType.ClimbLadder }),

                Zone("ROOF", ZoneKind.Roof, 4, Box(West, RoofFloor, East, CameraY + 5.625f), revealed: true,
                    connections: new[] { "Ladder_3F_B" }),
            };

            var blocks = new List<BlockData>
            {
                // 배경 (충돌 없음)
                Block("Building_Backdrop", Box(WestIn, F1, EastIn, RoofFloor - SlabThickness), Backdrop, solid: false, order: -10),

                // 바깥 지형
                Block("Ground", Box(-10f, CameraY - 5.625f, 10f, F1), Outer),
                Block("World_Left", Box(-10f, CameraY - 5.625f, -9.75f, CameraY + 5.625f), Outer),
                Block("World_Right", Box(9.75f, CameraY - 5.625f, 10f, CameraY + 5.625f), Outer),

                // 서쪽 외벽: 1층 정문 자리, 2층 창문 자리는 비워 둔다 (문·유리는 따로 만든다)
                Block("Wall_West_1F2F", Box(West, F1 + DoorHeight, WestIn, WindowBottom), Wall),
                Block("Wall_West_Upper", Box(West, WindowTop, WestIn, RoofFloor), Wall),
                Block("Wall_East", Box(EastIn, F1, East, RoofFloor), Wall),

                // 칸막이: 1층·3층은 아래쪽이 뚫린 통로, 2층은 문 자리
                Block("Partition_1F", Box(PartL, F1 + DoorHeight, PartR, F2 - SlabThickness), Wall),
                Block("Partition_2F", Box(PartL, F2 + DoorHeight, PartR, F3 - SlabThickness), Wall),
                Block("Partition_3F", Box(PartL, F3 + DoorHeight, PartR, RoofFloor - SlabThickness), Wall),
            };

            // 층 바닥(슬래브)과 사다리 구멍 덮개 (아래에서 위로만 통과)
            AddSlabWithHole(blocks, "Slab_2F", F2, 7.0f, 8.0f);
            AddSlabWithHole(blocks, "Slab_3F", F3, -4.5f, -3.5f);
            AddSlabWithHole(blocks, "Slab_Roof", RoofFloor, 7.0f, 8.0f);

            // 사다리 그림 (충돌 없음)
            blocks.Add(Block("Ladder_1F_B_Visual", Box(7.2f, F1, 7.8f, F2), Ladder, solid: false, order: -5));
            blocks.Add(Block("Ladder_2F_A_Visual", Box(-4.3f, F2, -3.7f, F3), Ladder, solid: false, order: -5));
            blocks.Add(Block("Ladder_3F_B_Visual", Box(7.2f, F3, 7.8f, RoofFloor), Ladder, solid: false, order: -5));

            var enemies = new List<EnemySpawn>
            {
                Goblin(-1.0f, F1, -1),
                Goblin(5.0f, F1, -1),
                Goblin(-2.5f, F2, -1),
                Goblin(4.5f, F2, -1),
                Goblin(-1.0f, F3, -1),
                Goblin(5.0f, F3, -1),
            };

            definition.EditorSet(zones, blocks, enemies, playerStart: new Vector2(-8f, F1), clearZoneId: "ROOF");
        }

        private static void AddSlabWithHole(List<BlockData> blocks, string name, float floorTop, float holeMin, float holeMax)
        {
            float bottom = floorTop - SlabThickness;
            blocks.Add(Block($"{name}_L", Box(West, bottom, holeMin, floorTop), Wall));
            blocks.Add(Block($"{name}_R", Box(holeMax, bottom, East, floorTop), Wall));
            blocks.Add(Block($"{name}_Hatch", Box(holeMin, floorTop - 0.1f, holeMax, floorTop), Hatch, oneWay: true));
        }

        private static Rect Box(float xMin, float yMin, float xMax, float yMax) =>
            Rect.MinMaxRect(xMin, yMin, xMax, yMax);

        private static ZoneData Zone(string id, ZoneKind kind, int floor, Rect bounds, bool revealed = false,
            string[] connections = null, ActionType[] affordances = null, int wallSide = 1) =>
            new ZoneData
            {
                Id = id,
                Kind = kind,
                Floor = floor,
                Bounds = bounds,
                RevealedAtStart = revealed,
                Connections = connections ?? new string[0],
                Affordances = affordances ?? new ActionType[0],
                WallSide = wallSide,
            };

        private static BlockData Block(string name, Rect bounds, Color color, bool solid = true, int order = 0,
            bool oneWay = false) =>
            new BlockData { Name = name, Bounds = bounds, Color = color, Solid = solid, SortingOrder = order, OneWay = oneWay };

        private static EnemySpawn Goblin(float x, float floor, int facing) =>
            new EnemySpawn { Kind = EnemyKind.Goblin, Position = new Vector2(x, floor), Facing = facing };
    }
}
