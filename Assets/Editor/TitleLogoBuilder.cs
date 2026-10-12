using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ReAction.Editor
{
    /// <summary>
    /// 메인 화면 제목 "Re:Action" 도트 로고와 안개 위 물음표를 코드로 그린다 (재현 가능, 도트 수정은 아래 글자 데이터만 고치면 된다).
    /// 겹 순서: 그림자 → 2도트 검정 외곽선 → 가로 띠 명암 채우기 → 윗면 하이라이트 → 아래·오른쪽 안쪽 음영.
    /// </summary>
    public static class TitleLogoBuilder
    {
        public const string LogoPath = "Assets/_Project/Art/UI/Title_ReAction.png";
        public const string FogMarkPath = "Assets/_Project/Art/UI/Fog_Question.png";

        private const int CapHeight = 20;
        private const int LetterSpacing = 2;
        private const int ShadowOffset = 2;
        private const int OutlineWidth = 2;

        // '#' = 채움. 글자는 아래(기준선)에 맞춰 놓인다. 대문자 20줄, 소문자 13줄, t·i는 18줄.
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['R'] = new[]
            {
                "###########...",
                "############..",
                "#############.",
                "###.......####",
                "###........###",
                "###........###",
                "###........###",
                "###.......####",
                "#############.",
                "############..",
                "###########...",
                "###....####...",
                "###.....####..",
                "###......####.",
                "###.......####",
                "###........###",
                "###........###",
                "###........###",
                "###........###",
                "###........###",
            },
            ['A'] = new[]
            {
                "....######....",
                "...########...",
                "..####..####..",
                ".####....####.",
                "####......####",
                "###........###",
                "###........###",
                "###........###",
                "###........###",
                "###........###",
                "##############",
                "##############",
                "##############",
                "###........###",
                "###........###",
                "###........###",
                "###........###",
                "###........###",
                "###........###",
                "###........###",
            },
            ['e'] = new[]
            {
                "..########..",
                ".##########.",
                "####....####",
                "###......###",
                "###......###",
                "############",
                "############",
                "###.........",
                "###.........",
                "####.....###",
                "####....####",
                ".##########.",
                "..########..",
            },
            ['c'] = new[]
            {
                "..########..",
                ".##########.",
                "####....####",
                "###......###",
                "###.........",
                "###.........",
                "###.........",
                "###.........",
                "###.........",
                "###......###",
                "####....####",
                ".##########.",
                "..########..",
            },
            ['o'] = new[]
            {
                "..########..",
                ".##########.",
                "####....####",
                "###......###",
                "###......###",
                "###......###",
                "###......###",
                "###......###",
                "###......###",
                "###......###",
                "####....####",
                ".##########.",
                "..########..",
            },
            ['n'] = new[]
            {
                "###.######..",
                "############",
                "#####...####",
                "####.....###",
                "###......###",
                "###......###",
                "###......###",
                "###......###",
                "###......###",
                "###......###",
                "###......###",
                "###......###",
                "###......###",
            },
            ['t'] = new[]
            {
                "..###....",
                "..###....",
                "..###....",
                "..###....",
                "..###....",
                "#########",
                "#########",
                "..###....",
                "..###....",
                "..###....",
                "..###....",
                "..###....",
                "..###....",
                "..###....",
                "..###....",
                "..###....",
                "..#######",
                "...######",
            },
            ['i'] = new[]
            {
                "###",
                "###",
                "###",
                "...",
                "...",
                "###",
                "###",
                "###",
                "###",
                "###",
                "###",
                "###",
                "###",
                "###",
                "###",
                "###",
                "###",
                "###",
            },
            ['?'] = new[]
            {
                "..########..",
                ".##########.",
                "####....####",
                "###......###",
                "###......###",
                ".........###",
                "........####",
                ".......####.",
                "......####..",
                ".....####...",
                ".....###....",
                ".....###....",
                ".....###....",
                "............",
                "............",
                ".....###....",
                ".....###....",
                ".....###....",
            },
            [':'] = new[]
            {
                "###",
                "###",
                "###",
                "...",
                "...",
                "...",
                "...",
                "...",
                "...",
                "...",
                "###",
                "###",
                "###",
            },
        };

        /// <summary>도트 글자의 색·테두리 설정.</summary>
        private sealed class Style
        {
            public Color32 Highlight;
            public Color32[] Bands;
            /// <summary>각 띠가 시작하는 줄 (글자 높이 기준)</summary>
            public int[] BandStarts;
            public Color32 InnerShade;
            public Color32 Outline;
            public int OutlineWidth;
            public Color32 Shadow;
            public int ShadowOffset;
        }

        // 붉은 글자 + 검정 테두리. 위가 밝고 아래가 어둡다.
        private static readonly Style LogoStyle = new Style
        {
            Highlight = Hex(0xFFE6DA),
            Bands = new[] { Hex(0xFF8270), Hex(0xFF5242), Hex(0xEE3A30), Hex(0xB82424) },
            BandStarts = new[] { 0, 5, 11, 16 },
            InnerShade = Hex(0x761414),
            Outline = Hex(0x000000),
            OutlineWidth = OutlineWidth,
            Shadow = new Color32(0x1A, 0x04, 0x04, 0xD0),
            ShadowOffset = ShadowOffset,
        };

        // 안개 위 물음표: 어두운 안개 위에서 튀지 않게 차분한 회색, 그림자 없음.
        private static readonly Style FogMarkStyle = new Style
        {
            Highlight = Hex(0xD4D7E2),
            Bands = new[] { Hex(0xA9ADBE), Hex(0x9095A8), Hex(0x787D92), Hex(0x62667A) },
            BandStarts = new[] { 0, 4, 9, 13 },
            InnerShade = Hex(0x4A4D5E),
            Outline = Hex(0x000000),
            OutlineWidth = 1,
            ShadowOffset = 0,
        };

        [MenuItem("ReAction/Build Title Logo")]
        public static void Build() => Paint("Re:Action", CapHeight, LogoStyle, LogoPath);

        /// <summary>가려진 방 가운데에 띄우는 물음표.</summary>
        [MenuItem("ReAction/Build Fog Mark")]
        public static void BuildFogMark() => Paint("?", Glyphs['?'].Length, FogMarkStyle, FogMarkPath);

        private static void Paint(string word, int glyphHeight, Style style, string assetPath)
        {
            bool[,] fill = Rasterize(word, glyphHeight, out bool[,] inside, out int glyphWidth);

            // 외곽선 + 그림자 여백
            int pad = style.OutlineWidth;
            int width = glyphWidth + pad * 2 + style.ShadowOffset;
            int height = glyphHeight + pad * 2 + style.ShadowOffset;
            var pixels = new Color32[width * height];

            bool Filled(int x, int y)
            {
                x -= pad;
                y -= pad;
                return x >= 0 && y >= 0 && x < glyphWidth && y < glyphHeight && fill[x, y];
            }

            bool Inside(int x, int y)
            {
                x -= pad;
                y -= pad;
                return x >= 0 && y >= 0 && x < glyphWidth && y < glyphHeight && inside[x, y];
            }

            // 외곽선 자리: 비어 있고 테두리 두께 안에 채움이 있다 (모서리는 둥글게).
            // 바깥은 OutlineWidth, 글자 안쪽(속·입구)은 1도트라 구멍이 메워지지 않는다.
            bool Near(int x, int y)
            {
                if (Filled(x, y)) return false;
                int w = Inside(x, y) ? 1 : style.OutlineWidth;
                for (int dy = -w; dy <= w; dy++)
                for (int dx = -w; dx <= w; dx++)
                {
                    if (w > 1 && Mathf.Abs(dx) == w && Mathf.Abs(dy) == w) continue;
                    if (Filled(x + dx, y + dy)) return true;
                }
                return false;
            }

            // y는 위에서부터 센다 (아래에서 PNG 행으로 뒤집는다)
            void Set(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) return;
                pixels[(height - 1 - y) * width + x] = c;
            }

            int o = style.ShadowOffset;
            if (o > 0)
            {
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (Filled(x - o, y - o) || Near(x - o, y - o)) Set(x, y, style.Shadow);
                }
            }

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (Near(x, y)) Set(x, y, style.Outline);
                else if (Filled(x, y)) Set(x, y, Shade(x, y, y - pad, Filled, style));
            }

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 32;
            importer.SaveAndReimport();
            Debug.Log($"[PZ] Pixel text built: {assetPath} ({width}x{height})");
        }

        private static Color32 Shade(int x, int y, int row, System.Func<int, int, bool> filled, Style style)
        {
            if (!filled(x, y - 1)) return style.Highlight;                       // 윗면
            if (!filled(x, y + 1) || !filled(x + 1, y)) return style.InnerShade; // 아랫면·오른면
            int band = 0;
            for (int i = 0; i < style.BandStarts.Length; i++)
            {
                if (row >= style.BandStarts[i]) band = i;
            }
            return style.Bands[band];
        }

        /// <summary>글자 채움과, 각 글자의 상자 안(속·입구 판정용)을 그린다. 글자는 아래(기준선)에 맞춘다.</summary>
        private static bool[,] Rasterize(string word, int height, out bool[,] inside, out int width)
        {
            var glyphs = word.Select(c => Glyphs[c]).ToList();
            width = glyphs.Sum(g => g[0].Length) + LetterSpacing * (glyphs.Count - 1);
            var fill = new bool[width, height];
            inside = new bool[width, height];
            int left = 0;
            foreach (var g in glyphs)
            {
                int top = height - g.Length;
                for (int r = 0; r < g.Length; r++)
                for (int c = 0; c < g[r].Length; c++)
                {
                    inside[left + c, top + r] = true;
                    if (g[r][c] == '#') fill[left + c, top + r] = true;
                }
                left += g[0].Length + LetterSpacing;
            }
            return fill;
        }

        private static Color32 Hex(int rgb) =>
            new Color32((byte)(rgb >> 16), (byte)(rgb >> 8 & 0xFF), (byte)(rgb & 0xFF), 0xFF);
    }
}
