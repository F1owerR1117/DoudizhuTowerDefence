using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PixelWasteland.EditorTools
{
    /// <summary>
    /// Level_6「废土高地」程序化像素美术生成器。
    /// 不依赖任何在线图像服务：用固定调色板 + Bayer 抖动直接画出 2D 像素废土背景与道具，
    /// 再把它们摆进 Level_6 场景的 LevelArt 根节点下。
    /// 菜单：Tools/PixelWasteland/Level6/Generate + Build
    /// </summary>
    public static class Level6ArtGenerator
    {
        const string ArtDir = "Assets/Art/Level6";
        const string ScenePath = "Assets/Scenes/Level_6.unity";

        // 世界坐标 → 像素换算（相机正交 400、宽 1422、背景图 640×360）
        const float WorldPerPixel = 1422f / 640f;   // 2.221875
        const float CamX = 300f;
        const float CamY = -50f;

        // ── 调色板（16 色废土） ──────────────────────
        static readonly Color32 S0 = new Color32(0x1A, 0x1D, 0x2C, 255); // 天顶
        static readonly Color32 S1 = new Color32(0x26, 0x2A, 0x40, 255);
        static readonly Color32 S2 = new Color32(0x3E, 0x3B, 0x52, 255);
        static readonly Color32 S3 = new Color32(0x6B, 0x55, 0x60, 255);
        static readonly Color32 S4 = new Color32(0xA2, 0x64, 0x4E, 255);
        static readonly Color32 S5 = new Color32(0xC9, 0x7F, 0x4F, 255); // 地平线余晖
        static readonly Color32 SUN = new Color32(0xF5, 0xD9, 0xA6, 255);
        static readonly Color32 CITY_FAR = new Color32(0x3A, 0x35, 0x50, 255);
        static readonly Color32 CITY_NEAR = new Color32(0x2E, 0x2B, 0x40, 255);
        static readonly Color32 RIDGE = new Color32(0x27, 0x24, 0x38, 255);
        static readonly Color32 RUIN = new Color32(0x4A, 0x42, 0x3C, 255);
        static readonly Color32 RUIN_DARK = new Color32(0x33, 0x2D, 0x2A, 255);
        static readonly Color32 DIRT = new Color32(0x6E, 0x5F, 0x45, 255);
        static readonly Color32 DIRT_DARK = new Color32(0x4A, 0x40, 0x34, 255);
        static readonly Color32 DIRT_LIGHT = new Color32(0x82, 0x70, 0x52, 255);
        static readonly Color32 ROAD = new Color32(0x6A, 0x60, 0x58, 255);
        static readonly Color32 ROAD_DARK = new Color32(0x52, 0x4A, 0x44, 255);
        static readonly Color32 ROAD_LINE = new Color32(0x9A, 0x92, 0x84, 255);
        static readonly Color32 RUST = new Color32(0x8A, 0x4E, 0x32, 255);
        static readonly Color32 TOXIC = new Color32(0x6F, 0xA9, 0x8C, 255);
        static readonly Color32 BONE = new Color32(0xC9, 0xBF, 0xAE, 255);
        static readonly Color32 FG = new Color32(0x24, 0x1F, 0x1C, 255);

        static Color32[] _px;
        static int _w, _h;
        static System.Random _rng;

        static readonly int[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        // ─────────────────────────────────────────────
        // 入口
        // ─────────────────────────────────────────────

        [MenuItem("Tools/PixelWasteland/Level6/Generate + Build")]
        public static void RunAll()
        {
            GenerateTextures();
            AssetDatabase.Refresh();
            ImportAll();
            BuildScene();
            Debug.Log("[Level6Art] done");
        }

        [MenuItem("Tools/PixelWasteland/Level6/Rebuild Scene Only")]
        public static void Rebuild()
        {
            BuildScene();
            Debug.Log("[Level6Art] scene rebuilt");
        }

        // ─────────────────────────────────────────────
        // 生成
        // ─────────────────────────────────────────────

        public static void GenerateTextures()
        {
            if (!Directory.Exists(ArtDir)) Directory.CreateDirectory(ArtDir);

            var bgPx = GenBackground(640, 360);
            Save("bg_wasteland", bgPx, 640, 360, 100f);
            SaveScaled("thumb_level6", bgPx, 640, 360, 320, 180);
            Save("prop_deadtree", GenDeadTree(48, 96), 48, 96, 100f);
            Save("prop_pole", GenPole(20, 110), 20, 110, 100f);
            Save("prop_barrel", GenBarrel(18, 26), 18, 26, 100f);
            Save("prop_tire", GenTire(20, 20), 20, 20, 100f);
            Save("prop_bush", GenBush(28, 16), 28, 16, 100f);
            Save("prop_sign", GenSign(26, 36), 26, 36, 100f);
            Save("prop_rock_s", GenRock(26, 16, 11), 26, 16, 100f);
            Save("prop_rock_l", GenRock(48, 30, 22), 48, 30, 100f);
            Save("prop_wreck", GenWreck(64, 28), 64, 28, 100f);
            Save("prop_rebar", GenRebar(40, 10), 40, 10, 100f);
            Save("prop_billboard", GenBillboard(70, 54), 70, 54, 100f);
        }

        // ── 背景 ───────────────────────────────────

        static Color32[] GenBackground(int w, int h)
        {
            _w = w; _h = h; _px = new Color32[w * h];
            _rng = new System.Random(20261125);

            const int roadTop = 178, roadBot = 146;

            Sky(359, 240);
            SunGlow(w - 170, 268, 14f);
            Clouds();
            Ridge(248);
            City(CITY_FAR, 244, 0.50f, 101);
            City(CITY_NEAR, 240, 0.62f, 202);
            HazeBand(230, 250, 0.5f);
            FarGround(194, 240);
            MidRuins(200);
            Shoulder(178, 202);
            RoadBand(roadBot, roadTop);
            CliffFace(32, 146);
            Guardrail(148);
            Foreground();
            Vignette();
            return _px;
        }

        static void Sky(int top, int bottom)
        {
            Color32[] ramp = { S5, S4, S3, S2, S1, S0 };
            for (int y = bottom; y <= top; y++)
            {
                float t = Mathf.Clamp01((y - bottom) / (float)(top - bottom));
                for (int x = 0; x < _w; x++)
                {
                    Set(x, y, RampDither(ramp, t, x, y));
                }
            }
            // 稀疏星点
            for (int i = 0; i < 90; i++)
            {
                int x = _rng.Next(0, _w), y = _rng.Next(300, 358);
                float a = (float)_rng.NextDouble() * 0.5f + 0.2f;
                Blend(x, y, new Color32(0xDD, 0xE2, 0xF0, 255), a * 0.55f);
            }
        }

        static void SunGlow(int cx, int cy, float r)
        {
            for (int y = -40; y <= 40; y++)
                for (int x = -40; x <= 40; x++)
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    if (d > 40f) continue;
                    int px = cx + x, py = cy + y;
                    if (d <= r)
                    {
                        Set(px, py, SUN);
                    }
                    else
                    {
                        float a = Mathf.Clamp01(1f - (d - r) / 36f);
                        a = a * a * 0.55f;
                        if (Dither(px, py, a)) Blend(px, py, S5, 0.35f);
                    }
                }
        }

        static void Clouds()
        {
            int[,] bands = { { 300, 8 }, { 288, 5 }, { 272, 4 }, { 258, 3 } };
            for (int b = 0; b < bands.GetLength(0); b++)
            {
                int y0 = bands[b, 0], hh = bands[b, 1];
                int x = _rng.Next(0, 120);
                while (x < _w)
                {
                    int len = _rng.Next(40, 130);
                    for (int i = 0; i < len; i++)
                        for (int k = 0; k < hh; k++)
                        {
                            int px = x + i, py = y0 + k;
                            float edge = Mathf.Min(i, len - i) / 12f;
                            float a = Mathf.Clamp01(edge) * 0.30f;
                            if (Dither(px, py, a)) Blend(px, py, S2, 0.5f);
                        }
                    x += len + _rng.Next(30, 110);
                }
            }
        }

        static void Ridge(int baseY)
        {
            for (int x = 0; x < _w; x++)
            {
                float n = ValueNoise(x * 0.012f, 3.3f, 77) * 16f + ValueNoise(x * 0.05f, 8.1f, 12) * 5f;
                int top = baseY - 4 - (int)n;
                for (int y = baseY - 26; y <= top; y++) Set(x, y, RIDGE);
            }
        }

        static void City(Color32 col, int baseY, float density, int seed)
        {
            var rng = new System.Random(seed);
            int x = -10;
            while (x < _w + 10)
            {
                int bw = rng.Next(8, 26);
                int bh = (int)(rng.Next(18, 78) * density);
                bool broken = rng.NextDouble() < 0.45;
                int topY = baseY + bh;
                for (int i = 0; i < bw; i++)
                {
                    int px = x + i;
                    if (px < 0 || px >= _w) continue;
                    int tt = topY;
                    if (broken) tt = topY - (int)(rng.NextDouble() * 14) - (i % 5 == 0 ? 6 : 0);
                    for (int y = baseY - 2; y <= tt; y++) Set(px, y, col);
                    // 亮边
                    if (i == 0)
                        for (int y = baseY; y <= tt; y++) Blend(px, y, S3, 0.35f);
                }
                // 天线
                if (rng.NextDouble() < 0.3 && bw > 10)
                {
                    int ax = x + bw / 2;
                    for (int y = topY; y < topY + 10; y++) Set(ax, y, col);
                }
                x += bw + rng.Next(2, 16);
            }
        }

        static void HazeBand(int y0, int y1, float a)
        {
            for (int y = y0; y <= y1; y++)
            {
                float t = (y - y0) / (float)Mathf.Max(1, y1 - y0);
                float aa = a * Mathf.Sin(t * Mathf.PI) * 0.6f;
                for (int x = 0; x < _w; x++)
                    if (Dither(x, y, aa)) Blend(x, y, S4, 0.45f);
            }
        }

        static void MidRuins(int baseY)
        {
            var rng = new System.Random(303);
            // 断续的残墙
            int x = -5;
            while (x < _w)
            {
                if (rng.NextDouble() < 0.55)
                {
                    int bw = rng.Next(14, 46);
                    int bh = rng.Next(6, 34);
                    for (int i = 0; i < bw; i++)
                    {
                        int px = x + i;
                        if (px < 0 || px >= _w) continue;
                        int jag = (int)(ValueNoise(px * 0.25f, 1.7f, 55) * 4f);
                        for (int y = baseY; y <= baseY + bh - jag; y++)
                            Set(px, y, y > baseY + bh - 3 ? RUIN : RUIN_DARK);
                    }
                    // 窗洞
                    for (int wy = baseY + 6; wy < baseY + bh - 6; wy += 7)
                        for (int wx = x + 4; wx < x + bw - 4; wx += 8)
                            if (rng.NextDouble() < 0.7)
                                for (int k = 0; k < 3; k++)
                                    for (int j = 0; j < 2; j++)
                                        Set(wx + j, wy + k, new Color32(0x1C, 0x18, 0x1A, 255));
                }
                x += rng.Next(10, 60);
            }

            // 右侧地标：断裂的输电塔
            Pylon(578, baseY, 96);
            // 左侧地标：倾倒的锈门
            RustGate(30, baseY);
            // 散落碎石
            for (int i = 0; i < 70; i++)
            {
                int px = rng.Next(0, _w);
                int py = baseY + rng.Next(0, 12);
                int s = rng.Next(1, 4);
                for (int k = 0; k < s; k++) Set(px + k, py, RUIN);
            }
        }

        static void Pylon(int cx, int baseY, int height)
        {
            int top = baseY + height;
            // 两条收拢的腿
            for (int y = baseY; y <= top; y++)
            {
                float t = (y - baseY) / (float)height;
                int half = (int)Mathf.Lerp(16, 3, t);
                Set(cx - half, y, RUIN);
                Set(cx + half, y, RUIN);
                if (y % 9 == 0) // 横撑
                    for (int x = cx - half; x <= cx + half; x++)
                        if (x % 3 != 0) Set(x, y, RUIN_DARK);
            }
            // 顶部横梁 + 警灯
            for (int x = cx - 5; x <= cx + 5; x++) Set(x, top, RUIN);
            for (int x = cx - 4; x <= cx + 4; x++) Set(x, top + 1, RUIN_DARK);
            Set(cx, top + 2, new Color32(0xD0, 0x57, 0x4A, 255));
            Blend(cx, top + 3, new Color32(0xD0, 0x57, 0x4A, 255), 0.5f);
            // 锈迹
            for (int i = 0; i < 26; i++)
            {
                int y = baseY + _rng.Next(0, height);
                float t = (y - baseY) / (float)height;
                int half = (int)Mathf.Lerp(16, 3, t);
                Blend(cx - half + _rng.Next(-1, 2), y, RUST, 0.5f);
            }
        }

        static void RustGate(int x0, int baseY)
        {
            // 两座门柱 + 倾倒的横梁
            Pillar(x0 + 4, baseY, 54);
            Pillar(x0 + 40, baseY, 46);
            for (int i = 0; i < 40; i++)
            {
                int x = x0 + 6 + i;
                int y = baseY + 40 - (int)(i * 0.35f);
                Set(x, y, RUIN); Set(x, y + 1, RUIN);
                if (i % 4 == 0) Blend(x, y - 1, RUST, 0.6f);
            }
            // 链条
            for (int i = 0; i < 14; i++)
                Blend(x0 + 22 + (i % 2), baseY + 30 - i, BONE, 0.25f);
        }

        static void Pillar(int x0, int baseY, int h)
        {
            for (int x = x0; x < x0 + 8; x++)
                for (int y = baseY; y < baseY + h; y++)
                    Set(x, y, (x - x0) % 4 == 0 ? RUIN_DARK : RUIN);
            for (int i = 0; i < 10; i++)
                Blend(x0 + _rng.Next(0, 8), baseY + _rng.Next(2, h), RUST, 0.45f);
        }

        /// <summary>城郊碎石旷野：把天际线与公路之间的空地填实，避免露出天空形成亮带。</summary>
        static void FarGround(int y0, int y1)
        {
            var rng = new System.Random(909);
            Color32 near = new Color32(0x3C, 0x35, 0x2C, 255);
            Color32 far = new Color32(0x28, 0x25, 0x2C, 255);
            for (int y = y0; y <= y1; y++)
            {
                float t = (y - y0) / (float)Mathf.Max(1, y1 - y0); // 1 = 远
                Color32 baseCol = Color32.Lerp(near, far, t);
                for (int x = 0; x < _w; x++)
                {
                    float n = ValueNoise(x * 0.03f, y * 0.09f, 61);
                    float n2 = ValueNoise(x * 0.008f, y * 0.02f, 62);
                    Color32 c = n + n2 * 0.5f > 0.72f ? Lighten(baseCol, 0.10f) : Darken(baseCol, n2 * 0.18f);
                    Set(x, y, RampDither(new Color32[] { c, Darken(c, 0.1f) }, n, x, y));
                }
            }
            // 远处堆着的废料与断墙残基
            for (int i = 0; i < 90; i++)
            {
                int x = rng.Next(0, _w);
                int y = rng.Next(y0 + 2, y1 - 3);
                int w = rng.Next(3, 14);
                int h = rng.Next(1, 4);
                Color32 c = Darken(near, 0.25f + (y - y0) / (float)(y1 - y0) * 0.3f);
                for (int k = 0; k < w; k++)
                    for (int j = 0; j < h; j++) Set(x + k, y + j, c);
            }
            // 远端地平线的雾
            for (int x = 0; x < _w; x++)
                for (int y = y1 - 6; y <= y1; y++)
                    if (Dither(x, y, 0.35f)) Blend(x, y, S4, 0.25f);
        }

        static void Shoulder(int y0, int y1)
        {
            for (int y = y0; y <= y1; y++)
            {
                float t = (y - y0) / (float)Mathf.Max(1, y1 - y0);
                for (int x = 0; x < _w; x++)
                {
                    float n = ValueNoise(x * 0.035f, y * 0.16f, 31);
                    float n2 = ValueNoise(x * 0.011f, y * 0.05f, 91);
                    Color32 col;
                    if (t < 0.25f) col = n > 0.5f ? DIRT_DARK : ROAD_DARK;
                    else if (t < 0.7f) col = (n + n2 * 0.6f) > 0.62f ? DIRT : DIRT_DARK;
                    else col = (n > 0.46f || n2 > 0.58f) ? DIRT : DIRT_DARK;
                    col = Darken(col, 0.12f);
                    Set(x, y, RampDither(new Color32[] { col, Darken(col, 0.14f) }, n2, x, y));
                }
            }
            var rng = new System.Random(404);
            // 干草簇
            for (int i = 0; i < 140; i++)
            {
                int x = rng.Next(0, _w);
                int y = rng.Next(y0 + 1, y1 - 1);
                Color32 g = new Color32(0x8C, 0x82, 0x4E, 255);
                int blades = rng.Next(2, 5);
                for (int k = 0; k < blades; k++)
                {
                    int h = rng.Next(2, 5);
                    for (int j = 0; j < h; j++) Blend(x + k - 2, y + j, j == h - 1 ? g : Darken(g, 0.25f), 0.75f);
                }
            }
            // 石砾
            for (int i = 0; i < 300; i++)
            {
                int x = rng.Next(0, _w), y = rng.Next(y0, y1);
                Blend(x, y, rng.NextDouble() < 0.5 ? RUIN : RUIN_DARK, 0.8f);
                if (rng.NextDouble() < 0.3) Blend(x + 1, y, RUIN_DARK, 0.5f);
            }
        }

        /// <summary>战斗线以下的断崖：路面像架在高地上，往下是岩层与乱石。</summary>
        static void CliffFace(int y0, int y1)
        {
            var rng = new System.Random(707);
            Color32 rockA = new Color32(0x4A, 0x3E, 0x35, 255);
            Color32 rockB = new Color32(0x3A, 0x30, 0x29, 255);
            Color32 rockC = new Color32(0x2B, 0x24, 0x20, 255);

            for (int y = y0; y <= y1; y++)
            {
                float t = (y - y0) / (float)Mathf.Max(1, y1 - y0); // 0 底 1 顶
                for (int x = 0; x < _w; x++)
                {
                    // 多尺度噪声决定的天然岩体，只有少量层理与裂纹
                    float n1 = ValueNoise(x * 0.006f, y * 0.010f, 21);
                    float n2 = ValueNoise(x * 0.022f, y * 0.034f, 22);
                    float n3 = ValueNoise(x * 0.070f, y * 0.090f, 23);
                    float v = n1 * 0.52f + n2 * 0.30f + n3 * 0.18f;

                    Color32 col = v > 0.60f ? rockA : (v > 0.42f ? rockB : rockC);
                    float band = Mathf.Sin(y * 0.42f + n1 * 7f);
                    if (band > 0.90f) col = Darken(col, 0.20f);          // 层理
                    if (n3 > 0.88f) col = Darken(col, 0.26f);            // 碎裂纹
                    else if (n3 < 0.12f) col = Lighten(col, 0.08f);      // 受光面
                    col = Darken(col, (1f - t) * 0.42f);
                    Set(x, y, RampDither(new Color32[] { col, Darken(col, 0.09f) }, n2, x, y));
                }
            }

            // 顶部路沿：一层被压实的土 + 碎石
            for (int x = 0; x < _w; x++)
            {
                int lip = y1 - 1 - (int)(ValueNoise(x * 0.05f, 1.3f, 21) * 2f);
                Set(x, lip, DIRT_LIGHT);
                Set(x, lip - 1, Darken(DIRT, 0.18f));
                Set(x, lip - 2, Darken(DIRT, 0.32f));
            }

            // 垂直裂缝
            for (int k = 0; k < 22; k++)
            {
                int cx = rng.Next(0, _w);
                int len = rng.Next(20, 80);
                int cy = y1 - rng.Next(0, 20);
                for (int i = 0; i < len && cy - i > y0; i++)
                {
                    Set(cx + (i / 14), cy - i, rockC);
                    Blend(cx + 1 + (i / 14), cy - i, new Color32(0x1E, 0x19, 0x16, 255), 0.5f);
                }
            }

            // 嵌在崖壁里的巨石（亮度贴近岩体，不抢眼）
            for (int k = 0; k < 16; k++)
            {
                int cx = rng.Next(6, _w - 6);
                int cy = rng.Next(y0 + 6, y1 - 10);
                int rx = rng.Next(5, 13), ry = rng.Next(3, 8);
                for (int y = -ry; y <= ry; y++)
                    for (int x = -rx; x <= rx; x++)
                    {
                        float d = (x * x) / (float)(rx * rx) + (y * y) / (float)(ry * ry);
                        if (d > 1f) continue;
                        Color32 c = (d > 0.78f) ? rockB : (y > ry - 3 ? rockB : rockC);
                        Blend(cx + x, cy + y, Darken(c, (1f - cy / (float)y1) * 0.4f), 0.55f);
                    }
            }

            // 崖壁上挂着的锈钢筋
            for (int k = 0; k < 6; k++)
            {
                int x = rng.Next(0, _w - 20), y = rng.Next(y0 + 20, y1 - 12);
                for (int i = 0; i < 16; i++) Blend(x + i, y - (i / 6), RUST, 0.8f);
            }

            // 崖底压黑，让画面下缘收得住
            for (int y = y0; y < y0 + 52; y++)
            {
                float a = 1f - (y - y0) / 52f;
                for (int x = 0; x < _w; x++) Blend(x, y, new Color32(0x14, 0x11, 0x10, 255), a * a * 0.62f);
            }
        }

        /// <summary>路面前沿的护栏：提供一条强有力的水平线，并暗示"高地"。</summary>
        static void Guardrail(int y)
        {
            var rng = new System.Random(808);
            int x = 6;
            while (x < _w)
            {
                int seg = rng.Next(26, 46);
                bool broken = rng.NextDouble() < 0.35;
                if (!broken)
                {
                    for (int i = 0; i < seg && x + i < _w; i++)
                    {
                        Blend(x + i, y, RUST, 0.85f);
                        Blend(x + i, y - 1, new Color32(0x5C, 0x38, 0x28, 255), 0.9f);
                        if (i % 3 == 0) Blend(x + i, y + 1, new Color32(0x2A, 0x22, 0x1E, 255), 0.6f);
                    }
                    if (x + seg < _w)
                        for (int p = 0; p < 5; p++)
                            Set(x + seg - 3, y - p, RUIN_DARK);
                }
                x += seg + rng.Next(6, 22);
            }
        }

        static void RoadBand(int y0, int y1)
        {
            var rng = new System.Random(505);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = 0; x < _w; x++)
                {
                    float n = ValueNoise(x * 0.05f, y * 0.4f, 71);
                    Color32 col = n > 0.55f ? ROAD : ROAD_DARK;
                    if (y == y1 || y == y0) col = ROAD_DARK;
                    Set(x, y, col);
                }
            }
            // 边缘碎石带
            for (int i = 0; i < 200; i++)
            {
                int x = rng.Next(0, _w);
                int y = rng.NextDouble() < 0.5 ? y0 - rng.Next(0, 4) : y1 + rng.Next(0, 4);
                Blend(x, y, RUIN_DARK, 0.7f);
            }
            // 磨损的白色虚线
            for (int x = 8; x < _w; x += 46)
                for (int i = 0; i < 18; i++)
                {
                    if (rng.NextDouble() < 0.18) continue;
                    int y = 162 + (rng.NextDouble() < 0.25 ? 1 : 0);
                    Blend(x + i, y, ROAD_LINE, 0.55f);
                    Blend(x + i, y + 1, ROAD_LINE, 0.35f);
                }
            // 裂缝
            for (int k = 0; k < 26; k++)
            {
                int x = rng.Next(0, _w), y = rng.Next(y0 + 2, y1 - 2);
                int len = rng.Next(6, 22);
                for (int i = 0; i < len; i++)
                {
                    x += rng.Next(0, 2);
                    y += rng.Next(-1, 2);
                    Set(x, y, new Color32(0x30, 0x2A, 0x26, 255));
                }
            }
            // 油渍
            for (int k = 0; k < 8; k++)
            {
                int cx = rng.Next(10, _w - 10), cy = rng.Next(y0 + 4, y1 - 4);
                int r = rng.Next(3, 7);
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                        if (x * x + y * y <= r * r && rng.NextDouble() < 0.75)
                            Blend(cx + x, cy + y, new Color32(0x2A, 0x25, 0x22, 255), 0.55f);
            }
            // 有毒积水
            int puddleX = 120, puddleY = y0 + 6;
            for (int y = -3; y <= 3; y++)
                for (int x = -12; x <= 12; x++)
                    if ((x * x) / 144f + (y * y) / 9f <= 1f)
                        Blend(puddleX + x, puddleY + y, TOXIC, 0.55f);

            // 车辙：两道压深的土轨
            for (int x = 0; x < _w; x++)
            {
                if (Dither(x, 0, 0.75f))
                {
                    Blend(x, y0 + 10, new Color32(0x4A, 0x42, 0x3C, 255), 0.55f);
                    Blend(x, y0 + 11, new Color32(0x3E, 0x37, 0x32, 255), 0.35f);
                    Blend(x, y0 + 22, new Color32(0x4A, 0x42, 0x3C, 255), 0.5f);
                }
            }
            // 沥青补丁
            for (int k = 0; k < 5; k++)
            {
                int px = rng.Next(0, _w - 30), py = rng.Next(y0 + 3, y1 - 6);
                int pw = rng.Next(16, 34), ph = rng.Next(4, 9);
                for (int y = py; y < py + ph; y++)
                    for (int x = px; x < px + pw; x++)
                        if (x < _w && y <= y1) Set(x, y, new Color32(0x5E, 0x55, 0x4E, 255));
            }
            // 坑洞
            for (int k = 0; k < 7; k++)
            {
                int cx = rng.Next(12, _w - 12), cy = rng.Next(y0 + 4, y1 - 3);
                int r = rng.Next(2, 5);
                for (int y = -r; y <= r; y++)
                    for (int x = -r * 2; x <= r * 2; x++)
                    {
                        float d = (x * x) / (float)(r * r * 4) + (y * y) / (float)(r * r);
                        if (d <= 1f) Set(cx + x, cy + y, new Color32(0x2C, 0x26, 0x22, 255));
                        else if (d <= 1.6f && y > 0) Blend(cx + x, cy + y, new Color32(0x84, 0x79, 0x6C, 255), 0.35f);
                    }
            }
        }

        static void Foreground()
        {
            var rng = new System.Random(606);
            // 最底部的乱石剪影（压低到画面下缘，只留一条）
            for (int x = 0; x < _w; x++)
            {
                float n = ValueNoise(x * 0.028f, 4.4f, 17);
                int top = 12 + (int)(n * 16f);
                for (int y = 0; y <= top; y++)
                {
                    Color32 c = y > top - 2 ? new Color32(0x33, 0x2C, 0x27, 255) : FG;
                    Set(x, y, c);
                }
            }
            // 前景大块混凝土（半埋）
            int[] bx = { 20, 168, 332, 470, 596 };
            for (int i = 0; i < bx.Length; i++)
            {
                int cx = bx[i];
                int cw = rng.Next(26, 56);
                int ch = rng.Next(10, 22);
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        int px = cx + x;
                        if (px < 0 || px >= _w) continue;
                        float jag = (x % 6 == 0) ? 2 : 0;
                        if (y > ch - jag) continue;
                        Set(px, y, y > ch - 3 ? new Color32(0x45, 0x3C, 0x34, 255) : FG);
                    }
            }
            // 钢筋
            for (int i = 0; i < 5; i++)
            {
                int x0 = rng.Next(0, _w - 40), y0 = rng.Next(4, 20);
                for (int j = 0; j < 34; j++)
                    Blend(x0 + j, y0 + (j / 10), RUST, 0.85f);
            }
        }

        static void Vignette()
        {
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    float dx = (x - _w * 0.5f) / (_w * 0.5f);
                    float dy = (y - _h * 0.5f) / (_h * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - 0.65f;
                    if (d > 0 && Dither(x, y, Mathf.Clamp01(d) * 0.9f))
                        Blend(x, y, new Color32(0x12, 0x10, 0x10, 255), Mathf.Clamp01(d) * 0.35f);
                }
        }

        // ═════════════════════════════════════════════
        // 关卡选择：末日幸存者营地
        // ═════════════════════════════════════════════

        static readonly Color32 FIRE_CORE = new Color32(0xFF, 0xE9, 0xC4, 255);
        static readonly Color32 FIRE_HOT = new Color32(0xFF, 0xD9, 0xA0, 255);
        static readonly Color32 FIRE_MID = new Color32(0xFF, 0xB3, 0x5C, 255);
        static readonly Color32 FIRE_LOW = new Color32(0xD2, 0x65, 0x3A, 255);
        static readonly Color32 CANVAS_TENT = new Color32(0x7A, 0x6B, 0x52, 255);
        static readonly Color32 CANVAS_DARK = new Color32(0x5E, 0x51, 0x3E, 255);
        static readonly Color32 METAL = new Color32(0x55, 0x4F, 0x48, 255);
        static readonly Color32 METAL_LIGHT = new Color32(0x6E, 0x66, 0x5C, 255);
        static readonly Color32 WOOD = new Color32(0x5A, 0x46, 0x33, 255);
        static readonly Color32 WOOD_LIGHT = new Color32(0x71, 0x59, 0x40, 255);

        public const int CampW = 640;
        public const int CampH = 360;
        // 篝火中心（像素坐标），供 UI 叠加动态火焰用
        public const int FirePxX = 402;
        public const int FirePxY = 94;

        [MenuItem("Tools/PixelWasteland/LevelSelect/Generate Camp Art")]
        public static void GenerateCampArt()
        {
            var bg = GenCampBackground(CampW, CampH);
            Save("bg_camp", bg, CampW, CampH, 100f);
            for (int i = 0; i < 4; i++) Save("campfire_" + i, GenCampFire(24, 26, i), 24, 26, 100f);
            AssetDatabase.Refresh();
            ImportAll();
        }

        [MenuItem("Tools/PixelWasteland/LevelSelect/Build Camp Background")]
        public static void BuildCampBackground()
        {
            GenerateCampArt();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/LevelSelect.unity", OpenSceneMode.Additive);
            var wasActive = SceneManager.GetActiveScene();

            GameObject canvasGo = null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == "Canvas") { canvasGo = roots[i]; break; }
                var t = roots[i].transform.Find("Canvas");
                if (t != null) { canvasGo = t.gameObject; break; }
            }
            if (canvasGo == null) { Debug.LogError("[Level6Art] 找不到 Canvas"); return; }

            var oldBg = canvasGo.transform.Find("BG_SurvivorCamp");
            if (oldBg != null) Object.DestroyImmediate(oldBg.gameObject);
            var oldScrim = canvasGo.transform.Find("BG_Scrim");
            if (oldScrim != null) Object.DestroyImmediate(oldScrim.gameObject);

            var spBg = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/bg_camp.png");
            if (spBg == null) { Debug.LogError("[Level6Art] bg_camp.png 未导入"); return; }

            // ── 背景 ──
            var bgGo = new GameObject("BG_SurvivorCamp", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.SetParent(canvasGo.transform, false);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgRt.SetSiblingIndex(0);

            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = spBg;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;
            bgImg.raycastTarget = false;

            var fit = bgGo.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = CampW / (float)CampH;

            // ── 动态篝火（叠在背景对应像素位置） ──
            var frames = new Sprite[4];
            for (int i = 0; i < 4; i++) frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/campfire_" + i + ".png");

            var fireGo = new GameObject("CampFire", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var fireRt = (RectTransform)fireGo.transform;
            fireRt.SetParent(bgRt, false);
            fireRt.anchorMin = new Vector2(FirePxX / (float)CampW, FirePxY / (float)CampH);
            fireRt.anchorMax = fireRt.anchorMin;
            fireRt.pivot = new Vector2(0.5f, 0.35f);
            fireRt.anchoredPosition = Vector2.zero;
            fireRt.sizeDelta = new Vector2(24f * (1920f / CampW), 26f * (1920f / CampW));

            var fireImg = fireGo.GetComponent<Image>();
            fireImg.raycastTarget = false;
            if (frames[0] != null) fireImg.sprite = frames[0];

            var anim = fireGo.AddComponent<SpriteFramePlayer>();
            anim.frames = frames;
            anim.framesPerSecond = 9f;

            // ── 压暗层，保证关卡卡片可读 ──
            var scrimGo = new GameObject("BG_Scrim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var scrimRt = (RectTransform)scrimGo.transform;
            scrimRt.SetParent(canvasGo.transform, false);
            scrimRt.anchorMin = Vector2.zero;
            scrimRt.anchorMax = Vector2.one;
            scrimRt.offsetMin = Vector2.zero;
            scrimRt.offsetMax = Vector2.zero;
            scrimRt.SetSiblingIndex(1);

            var scrimImg = scrimGo.GetComponent<Image>();
            scrimImg.color = new Color(0.04f, 0.04f, 0.06f, 0.26f);
            scrimImg.raycastTarget = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            if (wasActive.IsValid()) SceneManager.SetActiveScene(wasActive);
            Debug.Log("[Level6Art] 关卡选择背景已生成");
        }

        static Color32[] GenCampBackground(int w, int h)
        {
            _w = w; _h = h; _px = new Color32[w * h];
            _rng = new System.Random(20261126);

            Sky(359, 238);
            Clouds();
            Ridge(250);
            City(CITY_FAR, 246, 0.42f, 311);
            City(CITY_NEAR, 242, 0.52f, 322);
            HazeBand(228, 248, 0.45f);
            CampGround(0, 242);
            CampWall(150, 174);
            CampProps();
            FireGlow(FirePxX, FirePxY, 76f);
            CampForeground();
            Vignette();
            return _px;
        }

        static void CampGround(int y0, int y1)
        {
            var rng = new System.Random(4242);
            Color32 near = new Color32(0x45, 0x3B, 0x30, 255);
            Color32 far = new Color32(0x2D, 0x2A, 0x31, 255);
            for (int y = y0; y <= y1; y++)
            {
                float t = (y - y0) / (float)Mathf.Max(1, y1 - y0);
                Color32 baseCol = Color32.Lerp(near, far, Mathf.Pow(t, 0.75f));
                for (int x = 0; x < _w; x++)
                {
                    float n = ValueNoise(x * 0.012f, y * 0.022f, 61);
                    float m = ValueNoise(x * 0.07f, y * 0.14f, 62);
                    Color32 c = baseCol;
                    if (m > 0.74f) c = Lighten(c, 0.05f);
                    else if (m < 0.26f) c = Darken(c, 0.07f);
                    Set(x, y, RampDither(new Color32[] { c, Darken(c, 0.04f) }, n, x, y));
                }
            }
            // 被踩出来的土路（横向一条浅色带）
            for (int x = 0; x < _w; x++)
            {
                float band = ValueNoise(x * 0.01f, 2.2f, 71);
                int yb = 96 + (int)(band * 10f);
                for (int y = yb - 5; y <= yb + 5; y++)
                    if (Dither(x, y, 0.7f)) Blend(x, y, DIRT_LIGHT, 0.10f);
            }
            // 轮胎印
            for (int x = 0; x < _w; x++)
            {
                if (Dither(x, 0, 0.8f))
                {
                    Blend(x, 116, new Color32(0x24, 0x1F, 0x1C, 255), 0.35f);
                    Blend(x, 128, new Color32(0x24, 0x1F, 0x1C, 255), 0.30f);
                }
            }
            // 石砾与杂物（带上一点点投影，比纯噪点更像地面）
            for (int i = 0; i < 260; i++)
            {
                int x = rng.Next(0, _w), y = rng.Next(y0, y1 - 2);
                Blend(x, y, rng.NextDouble() < 0.5 ? RUIN : RUIN_DARK, 0.55f);
                Blend(x + 1, y - 1, new Color32(0x1E, 0x1A, 0x17, 255), 0.25f);
            }
            // 篝火周围被踩实的空地
            for (int y = -40; y <= 40; y++)
                for (int x = -120; x <= 120; x++)
                {
                    float d = (x * x) / (120f * 120f) + (y * y) / (40f * 40f);
                    if (d > 1f) continue;
                    if (Dither(FirePxX + x, FirePxY - 30 + y, (1f - d) * 0.5f))
                        Blend(FirePxX + x, FirePxY - 30 + y, DIRT_LIGHT, 0.10f);
                }
        }

        /// <summary>营地围墙：波纹铁皮 + 木柱 + 带刺铁丝网（正好落在画面中间那条冷带）。</summary>
        static void CampWall(int y0, int y1)
        {
            var rng = new System.Random(5151);
            int[] gaps = { 96, 322, 508 };   // 缺口位置
            int[] gapW = { 40, 34, 46 };

            for (int x = 0; x < _w; x++)
            {
                bool inGap = false;
                for (int g = 0; g < gaps.Length; g++)
                    if (x > gaps[g] && x < gaps[g] + gapW[g]) inGap = true;
                if (inGap) continue;

                int topJit = 1 + (int)(ValueNoise(x * 0.08f, 1.1f, 9) * 3f);
                for (int y = y0; y <= y1 - topJit; y++)
                {
                    bool rib = (x % 4 == 0);
                    Color32 c = rib ? METAL_LIGHT : METAL;
                    // 越靠下越暗（被地面阴影吞掉）
                    float d = (y - y0) / (float)Mathf.Max(1, y1 - y0);
                    c = Darken(c, (1f - d) * 0.45f);
                    Set(x, y, c);
                }
            }
            // 铁皮锈斑
            for (int i = 0; i < 120; i++)
            {
                int x = rng.Next(0, _w), y = rng.Next(y0 + 4, y1 - 6);
                Blend(x, y, rng.NextDouble() < 0.5 ? RUST : Darken(RUST, 0.3f), 0.6f);
                if (rng.NextDouble() < 0.4) Blend(x, y - 1, RUST, 0.35f);
            }
            // 木柱
            for (int x = 12; x < _w; x += 46)
            {
                for (int y = y0 - 4; y <= y1 + 3; y++)
                {
                    Set(x, y, WOOD);
                    Set(x + 1, y, WOOD_LIGHT);
                    Set(x + 2, y, WOOD);
                }
            }
            // 顶部铁丝网：两道，带刺
            for (int k = 0; k < 2; k++)
            {
                int wy = y1 + 6 + k * 6;
                for (int x = 0; x < _w; x++)
                {
                    float sag = Mathf.Sin(x * 0.02f + k) * 1.2f;
                    int yy = wy + (int)sag;
                    Blend(x, yy, new Color32(0x7A, 0x72, 0x66, 255), 0.75f);
                    if (x % 9 == 0) { Blend(x, yy + 1, BONE, 0.45f); Blend(x, yy - 1, BONE, 0.35f); }
                }
            }
            // 缺口两侧的门框（木）
            for (int g = 0; g < gaps.Length; g++)
            {
                int gx = gaps[g];
                for (int y = y0; y <= y1 + 2; y++)
                {
                    Set(gx - 2, y, WOOD); Set(gx - 1, y, WOOD_LIGHT);
                    Set(gx + gapW[g] + 1, y, WOOD_LIGHT); Set(gx + gapW[g] + 2, y, WOOD);
                }
            }
        }

        static void CampProps()
        {
            Tent(56, 70, 108, 72);
            Tent(186, 84, 70, 46);
            Tent(468, 74, 96, 62);
            Tarp(300, 104, 50, 26);
            CrateStack(150, 62);
            Barrels(252, 70, 3);
            CrateStack(262, 62);
            MapTable(138, 50);
            WatchTower(586, 62, 76, 44);
            StringLights(140, 540, 136);
            Laundry(300, 360, 120);
            FireRing(FirePxX, FirePxY);
            Sitter(370, 82, 1);
            Sitter(438, 80, -1);
            Sandbags(0, 30, 100);
            Sandbags(486, 26, 84);
            SignPost(214, 66);
            RadioTower(516, 246, 54);
            SmokeColumn(96, 242, 120);
            SmokeColumn(430, 240, 96);
        }

        /// <summary>篝火旁坐着的幸存者剪影（有火光侧的暖边）。</summary>
        static void Sitter(int x, int y, int facing)
        {
            Color32 body = new Color32(0x22, 0x1C, 0x19, 255);
            // 身体（坐着，前倾）
            for (int j = 0; j < 8; j++)
            {
                int wdt = 5 + (j > 4 ? 2 : 0);
                for (int i = 0; i < wdt; i++)
                    Set(x + i * facing * -1 + (facing > 0 ? 0 : wdt - 1), y + j, body);
            }
            // 头
            int hx = x + (facing > 0 ? 1 : -1);
            for (int a = -1; a <= 1; a++)
                for (int b = 0; b < 3; b++) Set(hx + a, y + 8 + b, body);
            // 火侧暖边
            for (int j = 0; j < 9; j++)
            {
                int rx = x + (facing > 0 ? 0 : -1);
                Blend(rx, y + j, FIRE_LOW, 0.45f);
            }
        }

        static void Tent(int x, int y, int w, int h)
        {
            int half = w / 2;
            bool litRight = (x + half) < FirePxX;   // 帐篷在火左边 → 右面受光
            for (int i = 0; i < w; i++)
            {
                float t = Mathf.Abs(i - half) / (float)half;
                int hh = (int)(h * (1f - t * 0.94f));
                bool litSide = litRight ? (i > half) : (i < half);
                for (int j = 0; j < hh; j++)
                {
                    Color32 c = CANVAS_TENT;
                    if (j > hh * 0.6f) c = Lighten(c, 0.05f);
                    if (!litSide) c = Darken(c, 0.20f);
                    else if (j < hh * 0.5f) Blend(x + i, y + j, FIRE_MID, 0.06f);
                    if (j < 2) c = CANVAS_DARK;
                    Set(x + i, y + j, c);
                }
                Blend(x + i, y + hh - 1, BONE, 0.10f);
            }
            // 门洞：窄 A 形，里面透出一点火光
            int doorW = 6;
            for (int j = 1; j < h * 0.45f; j++)
            {
                int dw = Mathf.Max(1, doorW - j / 10);
                for (int i = -dw; i <= dw; i++)
                {
                    int px = x + half + i, py = y + j;
                    Set(px, py, new Color32(0x33, 0x26, 0x1C, 255));
                    if (j < 4) Blend(px, py, FIRE_LOW, 0.35f);
                }
            }
            // 补丁
            var rng = new System.Random(x * 7 + y);
            for (int k = 0; k < 5; k++)
            {
                int px = x + rng.Next(6, w - 8), py = y + rng.Next(6, h - 12);
                for (int a = 0; a < 4; a++)
                    for (int b = 0; b < 3; b++)
                        Blend(px + a, py + b, new Color32(0x6E, 0x62, 0x4A, 255), 0.8f);
            }
            // 脊杆 + 拉绳 + 地钉
            for (int j = 0; j < 7; j++) Set(x + half, y + h + j, WOOD_LIGHT);
            for (int k = 0; k < 14; k++) Blend(x + half - k, y + 2 - k / 7, BONE, 0.10f);
            for (int k = 0; k < 14; k++) Blend(x + half + k, y + 2 - k / 7, BONE, 0.10f);
            Blend(x - 8, y + 1, BONE, 0.5f); Blend(x + w + 7, y + 1, BONE, 0.5f);
        }

        static void Tarp(int x, int y, int w, int h)
        {
            for (int i = 0; i < w; i++)
            {
                int hh = (int)(h * Mathf.Sin((i / (float)w) * Mathf.PI));
                for (int j = 0; j < hh; j++)
                    Set(x + i, y + j, j > hh - 2 ? CANVAS_DARK : new Color32(0x4E, 0x53, 0x4A, 255));
            }
            for (int j = 0; j < h; j += 6) Set(x, y + j, WOOD);
            Set(x + w - 1, y, WOOD); Set(x + w - 1, y + h - 1, WOOD);
        }

        static void CrateStack(int x, int y)
        {
            Box(x, y, 20, 16, WOOD, WOOD_LIGHT);
            Box(x + 16, y, 18, 14, Darken(WOOD, 0.12f), WOOD_LIGHT);
            Box(x + 6, y + 16, 18, 15, WOOD, WOOD_LIGHT);
            // 板条
            for (int i = 0; i < 20; i++) Blend(x + i, y + 8, Darken(WOOD, 0.35f), 0.7f);
            for (int i = 0; i < 18; i++) Blend(x + 6 + i, y + 23, Darken(WOOD, 0.35f), 0.7f);
        }

        static void Box(int x, int y, int w, int h, Color32 fill, Color32 edge)
        {
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                {
                    Color32 c = (i == 0 || i == w - 1 || j == 0 || j == h - 1) ? edge : fill;
                    Set(x + i, y + j, c);
                }
        }

        static void Barrels(int x, int y, int n)
        {
            for (int k = 0; k < n; k++)
            {
                int bx = x + k * 10;
                for (int i = 0; i < 8; i++)
                    for (int j = 0; j < 12; j++)
                    {
                        Color32 c = (i == 0 || i == 7) ? Darken(RUST, 0.4f) : RUST;
                        if (j == 4 || j == 8) c = Darken(RUST, 0.35f);
                        Set(bx + i, y + j, c);
                    }
            }
        }

        static void MapTable(int x, int y)
        {
            for (int i = 0; i < 26; i++) for (int j = 0; j < 2; j++) Set(x + i, y + 8 + j, WOOD_LIGHT);
            for (int k = 0; k < 2; k++)
            {
                for (int j = 0; j < 8; j++) { Set(x + 2 + k * 20, y + j, WOOD); Set(x + 3 + k * 20, y + j, Darken(WOOD, 0.2f)); }
            }
            for (int i = 0; i < 20; i++) for (int j = 0; j < 7; j++) Blend(x + 3 + i, y + 11 + j, new Color32(0xB6, 0xAA, 0x8C, 255), 0.75f);
            for (int i = 0; i < 6; i++) Blend(x + 6 + i, y + 13 + (i % 2), new Color32(0x6A, 0x62, 0x4A, 255), 0.6f);
        }

        static void WatchTower(int x, int baseY, int height, int width)
        {
            int topY = baseY + height;
            // 四根腿（近大远小的错觉：两条主腿 + 两条细腿）
            for (int j = 0; j < height; j++)
            {
                float t = j / (float)height;
                int lx = x + (int)(t * 8);
                int rx = x + width - (int)(t * 8);
                Set(lx, baseY + j, WOOD); Set(lx + 1, baseY + j, WOOD_LIGHT);
                Set(rx, baseY + j, WOOD); Set(rx + 1, baseY + j, WOOD_LIGHT);
                if (j % 14 == 0)
                    for (int i = lx; i <= rx; i++) Set(i, baseY + j, Darken(WOOD, 0.15f));
            }
            // 平台
            for (int i = -4; i < width + 6; i++)
                for (int j = 0; j < 4; j++) Set(x + i, topY + j, j == 3 ? WOOD_LIGHT : WOOD);
            // 护栏
            for (int i = -4; i < width + 6; i++) Set(x + i, topY + 12, WOOD_LIGHT);
            for (int i = -4; i < width + 6; i += 6) Set(x + i, topY + 4, WOOD);
            // 顶棚
            for (int i = -8; i < width + 10; i++)
            {
                int hh = 6 - Mathf.Abs(i) / 12;
                for (int j = 0; j < 3 + hh; j++) Set(x + i, topY + 13 + j, j % 2 == 0 ? METAL : METAL_LIGHT);
            }
            // 梯子
            for (int j = 0; j < height; j += 1)
            {
                if (j % 5 == 0)
                {
                    Set(x + 10, baseY + j, WOOD_LIGHT);
                    Set(x + 22, baseY + j, WOOD_LIGHT);
                }
            }
            // 挂灯（暖光点）
            int lx2 = x + width - 6, ly2 = topY + 16;
            Set(lx2, ly2, FIRE_HOT); Set(lx2 + 1, ly2, FIRE_MID); Set(lx2, ly2 + 1, FIRE_MID);
            for (int a = -3; a <= 3; a++)
                for (int b = -3; b <= 3; b++)
                    if (a * a + b * b <= 9 && Dither(lx2 + a, ly2 + b, 0.5f))
                        Blend(lx2 + a, ly2 + b, FIRE_MID, 0.10f);
        }

        static void StringLights(int x0, int x1, int y)
        {
            for (int x = x0; x <= x1; x++)
            {
                float t = (x - x0) / (float)Mathf.Max(1, x1 - x0);
                int sag = (int)(Mathf.Sin(t * Mathf.PI) * 9f);
                int yy = y - sag;
                Blend(x, yy, new Color32(0x8A, 0x82, 0x74, 255), 0.35f);
                if ((x - x0) % 22 == 0)
                {
                    Set(x, yy - 1, FIRE_HOT);
                    Set(x, yy - 2, FIRE_MID);
                    for (int a = -2; a <= 2; a++)
                        for (int b = -3; b <= 1; b++)
                            if (a * a + b * b <= 5 && Dither(x + a, yy + b, 0.45f))
                                Blend(x + a, yy + b, FIRE_MID, 0.09f);
                }
            }
        }

        static void Laundry(int x0, int x1, int y)
        {
            for (int x = x0; x <= x1; x++)
            {
                float t = (x - x0) / (float)Mathf.Max(1, x1 - x0);
                int yy = y - (int)(Mathf.Sin(t * Mathf.PI) * 5f);
                Blend(x, yy, BONE, 0.30f);
            }
            int[] px = { x0 + 10, x0 + 26, x0 + 46 };
            int[] pw = { 10, 8, 12 };
            int[] ph = { 14, 10, 12 };
            Color32[] cols = { new Color32(0x9A, 0x8E, 0x76, 255), new Color32(0x6E, 0x7A, 0x72, 255), new Color32(0x8A, 0x6E, 0x5C, 255) };
            for (int k = 0; k < 3; k++)
                for (int i = 0; i < pw[k]; i++)
                    for (int j = 0; j < ph[k]; j++)
                        Set(px[k] + i, y - 4 - j, j == ph[k] - 1 ? Darken(cols[k], 0.2f) : cols[k]);
        }

        static void FireRing(int cx, int cy)
        {
            // 石圈
            for (int a = 0; a < 360; a += 18)
            {
                float rad = a * Mathf.Deg2Rad;
                int x = cx + (int)(Mathf.Cos(rad) * 13f);
                int y = cy + (int)(Mathf.Sin(rad) * 6f);
                for (int i = -1; i <= 1; i++) Set(x + i, y, i == 0 ? RUIN : RUIN_DARK);
            }
            // 柴
            for (int i = -9; i <= 9; i++)
            {
                Set(cx + i, cy - 1, WOOD);
                Set(cx + i, cy, Darken(WOOD, 0.25f));
            }
            for (int i = -6; i <= 6; i++) Blend(cx + i, cy + 2, WOOD_LIGHT, 0.7f);
            // 三脚架 + 吊锅
            for (int j = 0; j < 16; j++)
            {
                Set(cx - 8 + j / 4, cy + 3 + j, Darken(WOOD, 0.1f));
                Set(cx + 8 - j / 4, cy + 3 + j, Darken(WOOD, 0.1f));
            }
            for (int i = -4; i <= 4; i++) Set(cx + i, cy + 15, Darken(METAL, 0.2f));
            for (int i = -3; i <= 3; i++) Set(cx + i, cy + 12, METAL);
        }

        static void Sandbags(int x, int y, int w)
        {
            for (int row = 0; row < 3; row++)
            {
                int off = (row % 2) * 5;
                for (int i = 0; i < w; i += 10)
                {
                    int bx = x + i + off;
                    for (int a = 0; a < 10; a++)
                        for (int b = 0; b < 4; b++)
                        {
                            bool edge = (a == 0 || a == 9 || b == 0 || b == 3);
                            Set(bx + a, y + row * 4 + b, edge ? Darken(DIRT_DARK, 0.35f) : DIRT_DARK);
                        }
                }
            }
        }

        static void SignPost(int x, int y)
        {
            for (int j = 0; j < 26; j++) { Set(x, y + j, WOOD); Set(x + 1, y + j, WOOD_LIGHT); }
            for (int i = 0; i < 22; i++)
                for (int j = 0; j < 9; j++)
                    Set(x - 6 + i, y + 20 + j, j == 0 || j == 8 ? Darken(WOOD, 0.3f) : new Color32(0x8A, 0x7A, 0x5E, 255));
            // 涂鸦箭头
            for (int i = 0; i < 8; i++) Set(x - 3 + i, y + 24, new Color32(0x2C, 0x24, 0x1E, 255));
            for (int i = 0; i < 3; i++) { Set(x + 5, y + 23 + i, new Color32(0x2C, 0x24, 0x1E, 255)); Set(x + 4 - i, y + 25 - i, new Color32(0x2C, 0x24, 0x1E, 255)); }
        }

        static void RadioTower(int cx, int baseY, int height)
        {
            int top = baseY + height;
            for (int y = baseY; y <= top; y++)
            {
                float t = (y - baseY) / (float)height;
                int half = (int)Mathf.Lerp(9, 2, t);
                Set(cx - half, y, CITY_NEAR);
                Set(cx + half, y, CITY_NEAR);
                if (y % 7 == 0)
                    for (int x = cx - half; x <= cx + half; x++) if (x % 2 == 0) Set(x, y, CITY_NEAR);
            }
            Set(cx, top + 1, new Color32(0xD0, 0x57, 0x4A, 255));
            Blend(cx, top + 2, new Color32(0xD0, 0x57, 0x4A, 255), 0.45f);
        }

        static void SmokeColumn(int x, int baseY, int height)
        {
            for (int j = 0; j < height; j++)
            {
                float t = j / (float)height;
                int wdt = (int)Mathf.Lerp(4, 16, t);
                int xx = x + (int)(Mathf.Sin(j * 0.09f) * (6f + t * 10f));
                for (int i = -wdt / 2; i <= wdt / 2; i++)
                    if (Dither(xx + i, baseY + j, 0.28f * (1f - t)))
                        Blend(xx + i, baseY + j, new Color32(0x6A, 0x62, 0x5C, 255), 0.18f);
            }
        }

        /// <summary>篝火的暖光晕（加色抖动），背景里只画光，火苗由 UI 上的 4 帧动画提供。</summary>
        static void FireGlow(int cx, int cy, float r)
        {
            for (int y = (int)-r; y <= (int)r; y++)
                for (int x = (int)-r; x <= (int)r; x++)
                {
                    float d = Mathf.Sqrt(x * x + y * y * 1.35f) / r;
                    if (d > 1f) continue;
                    float a = 1f - d;
                    a = a * a;
                    // 地面比空气更吃光：下半部分再压一点亮度
                    if (y < 0) a *= 0.85f;
                    if (Dither(cx + x, cy + y, a * 0.95f))
                    {
                        Color32 tint = d < 0.22f ? FIRE_CORE : (d < 0.42f ? FIRE_HOT : FIRE_MID);
                        Blend(cx + x, cy + y, tint, a * 0.55f);
                    }
                }
        }

        static void CampForeground()
        {
            var rng = new System.Random(9090);
            for (int x = 0; x < _w; x++)
            {
                float n = ValueNoise(x * 0.03f, 4.4f, 17);
                int top = 10 + (int)(n * 14f);
                for (int y = 0; y <= top; y++)
                    Set(x, y, y > top - 2 ? new Color32(0x33, 0x2C, 0x27, 255) : FG);
            }
            int[] bx = { 40, 240, 420, 560 };
            for (int i = 0; i < bx.Length; i++)
            {
                int cw = rng.Next(24, 50), ch = rng.Next(8, 16);
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                        Set(bx[i] + x, y, y > ch - 3 ? new Color32(0x45, 0x3C, 0x34, 255) : FG);
            }
        }

        /// <summary>篝火火苗的一帧（透明底，供 UI 叠加）。</summary>
        static Color32[] GenCampFire(int w, int h, int frame)
        {
            Begin(w, h, 7711 + frame);
            int cx = w / 2;
            int hgt = 12 + (frame % 2 == 0 ? 3 : 0) + (frame == 3 ? -2 : 0);

            for (int j = 0; j < hgt; j++)
            {
                float t = j / (float)hgt;
                int halfw = Mathf.Max(1, (int)((1f - t) * 6f + Mathf.Sin(j * 0.9f + frame) * 1.4f));
                for (int i = -halfw; i <= halfw; i++)
                {
                    Color32 c;
                    if (t > 0.78f) c = FIRE_MID;
                    else if (t > 0.45f) c = FIRE_HOT;
                    else c = FIRE_CORE;
                    if (Mathf.Abs(i) >= halfw - 1) c = FIRE_LOW;
                    Set(cx + i, 3 + j, c);
                }
            }
            // 火星
            for (int k = 0; k < 5; k++)
            {
                int sx = cx + _rng.Next(-6, 7);
                int sy = 8 + _rng.Next(0, hgt + 4);
                Set(sx, sy, k % 2 == 0 ? FIRE_MID : FIRE_LOW);
            }
            return _px;
        }

        // ── 道具 ───────────────────────────────────

        static Color32[] GenDeadTree(int w, int h)
        {
            Begin(w, h, 2026);
            int cx = w / 2;
            // 主干：底部有根瘤，向上收细
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)h;
                int half = Mathf.Max(1, (int)Mathf.Lerp(5.5f, 1.4f, Mathf.Sqrt(t)));
                for (int x = -half; x <= half; x++)
                {
                    Color32 c = x >= half - 1 ? RUIN_DARK : (x <= -half + 1 ? Darken(RUIN, 0.25f) : RUIN);
                    Set(cx + x, y, c);
                }
            }
            // 根部外扩
            for (int y = 0; y < 7; y++)
                for (int x = -8; x <= 8; x++)
                    if (Mathf.Abs(x) > 5 - y / 3) Set(cx + x, y, Darken(RUIN, 0.2f));

            Branch(cx, (int)(h * 0.92f), 1f, 10);
            Branch(cx, (int)(h * 0.78f), 1f, 14);
            Branch(cx, (int)(h * 0.66f), -1f, 13);
            Branch(cx, (int)(h * 0.55f), 1f, 11);
            Branch(cx, (int)(h * 0.47f), -1f, 9);
            return _px;
        }

        static void Branch(int cx, int cy, float dir, int len)
        {
            float ang = Mathf.Lerp(0.35f, 1.05f, (float)_rng.NextDouble());
            float x = cx, y = cy;
            for (int i = 0; i < len; i++)
            {
                x += dir * Mathf.Lerp(0.5f, 1.3f, i / (float)len);
                y += ang;
                int px = (int)x, py = (int)y;
                Set(px, py, RUIN);
                Set(px + 1, py, Darken(RUIN, 0.2f));
                Set(px, py - 1, RUIN); // 加粗
                if (i == len / 2 || i == len - 2)
                {
                    float x2 = x, y2 = y;
                    float a2 = ang * 0.6f;
                    int l2 = Mathf.Max(3, len / 2 - 1);
                    for (int j = 0; j < l2; j++)
                    {
                        x2 += dir * 0.7f; y2 += a2;
                        Set((int)x2, (int)y2, RUIN);
                        Set((int)x2, (int)y2 - 1, RUIN);
                    }
                }
            }
        }

        static Color32[] GenPole(int w, int h)
        {
            Begin(w, h, 77);
            int cx = w / 2;
            for (int y = 0; y < h - 14; y++)
                for (int x = -1; x <= 1; x++) Set(cx + x, y, x == 0 ? RUIN : RUIN_DARK);
            for (int x = -6; x <= 6; x++)
                for (int y = h - 18; y <= h - 16; y++) Set(cx + x, y, RUIN);
            // 绝缘子
            Set(cx - 6, h - 19, BONE); Set(cx + 6, h - 19, BONE);
            // 断线
            for (int i = 0; i < 10; i++) Blend(cx + 6 + i, h - 20 - (i / 3), BONE, 0.4f);
            return _px;
        }

        static Color32[] GenBarrel(int w, int h)
        {
            Begin(w, h, 31);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool edge = x <= 1 || x >= w - 2;
                    Color32 c = edge ? RUIN_DARK : (x == 2 ? new Color32(0xA0, 0x62, 0x42, 255) : RUST);
                    if (y == 5 || y == h - 6) c = RUIN_DARK;
                    Set(x, y, c);
                }
            for (int i = 0; i < 12; i++)
                Blend(_rng.Next(2, w - 2), _rng.Next(0, h), new Color32(0x5C, 0x38, 0x28, 255), 0.6f);
            return _px;
        }

        static Color32[] GenTire(int w, int h)
        {
            Begin(w, h, 12);
            float cx = (w - 1) / 2f, cy = (h - 1) / 2f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d <= 9.2f && d >= 3.6f) Set(x, y, d > 8.4f ? new Color32(0x2A, 0x26, 0x24, 255) : new Color32(0x38, 0x33, 0x30, 255));
                }
            return _px;
        }

        static Color32[] GenBush(int w, int h)
        {
            Begin(w, h, 9);
            Color32 g = new Color32(0x8C, 0x82, 0x4E, 255);
            for (int i = 0; i < 14; i++)
            {
                int x = _rng.Next(1, w - 1);
                int hh = _rng.Next(3, h - 1);
                for (int j = 0; j < hh; j++) Set(x, j, j > hh - 3 ? g : new Color32(0x6E, 0x64, 0x3C, 255));
            }
            return _px;
        }

        static Color32[] GenSign(int w, int h)
        {
            Begin(w, h, 42);
            for (int y = 0; y < h - 16; y++)
                for (int x = w / 2 - 1; x <= w / 2 + 1; x++) Set(x, y, RUIN);
            for (int y = h - 18; y < h; y++)
                for (int x = 2; x < w - 2; x++)
                {
                    int px = x - 2;
                    if (y == h - 1 && px % 2 == 0) continue;
                    Set(x, y, y >= h - 3 ? RUST : new Color32(0xC9, 0x8A, 0x3C, 255));
                }
            for (int x = 5; x < w - 5; x += 3) Set(x, h - 8, new Color32(0x2A, 0x24, 0x1E, 255));
            return _px;
        }

        static Color32[] GenRock(int w, int h, int seed)
        {
            Begin(w, h, seed);
            float cx = (w - 1) / 2f;
            for (int x = 0; x < w; x++)
            {
                float t = Mathf.Abs(x - cx) / cx;
                int top = (int)(h * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * (0.75f + 0.25f * ValueNoise(x * 0.3f, 1.1f, seed)));
                for (int y = 0; y < Mathf.Min(top, h); y++)
                {
                    Color32 c = y > top - 3 ? new Color32(0x5E, 0x54, 0x48, 255) : (y < 2 ? RUIN_DARK : RUIN);
                    Set(x, y, c);
                }
            }
            return _px;
        }

        static Color32[] GenWreck(int w, int h)
        {
            Begin(w, h, 88);
            int bodyTop = h - 14;
            for (int x = 4; x < w - 4; x++)
            {
                int roof = (x > 20 && x < 46) ? h - 22 : bodyTop;
                for (int y = 4; y < roof; y++)
                {
                    Color32 c = (y > roof - 3) ? new Color32(0x9A, 0x5C, 0x3C, 255) : RUST;
                    Set(x, y, c);
                }
            }
            // 轮位缺口
            for (int x = 10; x < 20; x++) for (int y = 0; y < 5; y++) Set(x, y, new Color32(0, 0, 0, 0));
            for (int x = w - 22; x < w - 12; x++) for (int y = 0; y < 5; y++) Set(x, y, new Color32(0, 0, 0, 0));
            // 破窗
            for (int x = 24; x < 42; x++) for (int y = h - 21; y < h - 15; y++) Set(x, y, new Color32(0x22, 0x1E, 0x1C, 255));
            return _px;
        }

        static Color32[] GenRebar(int w, int h)
        {
            Begin(w, h, 55);
            for (int x = 1; x < w - 1; x++)
            {
                int y = 3 + (int)(Mathf.Sin(x * 0.22f) * 1.6f);
                Set(x, y, RUST);
                Set(x, y + 1, new Color32(0x5C, 0x38, 0x28, 255));
            }
            return _px;
        }

        static Color32[] GenBillboard(int w, int h)
        {
            Begin(w, h, 66);
            // 支腿
            for (int y = 0; y < h - 22; y++)
            {
                Set(w / 2 - 6, y, RUIN_DARK); Set(w / 2 - 5, y, RUIN_DARK);
                Set(w / 2 + 6, y, RUIN_DARK); Set(w / 2 + 5, y, RUIN_DARK);
            }
            // 板面（歪斜）
            for (int y = h - 22; y < h - 2; y++)
            {
                int shift = (h - 2 - y) / 6;
                for (int x = 3; x < w - 4; x++)
                {
                    int px = x + shift;
                    Color32 c = (y == h - 3 || y == h - 22) ? RUIN_DARK : new Color32(0x7A, 0x72, 0x60, 255);
                    if (y > h - 12 && y < h - 6 && x > 10 && x < w - 12) c = new Color32(0x4A, 0x42, 0x36, 255);
                    Set(px, y, c);
                }
            }
            return _px;
        }

        // ─────────────────────────────────────────────
        // 导入设置
        // ─────────────────────────────────────────────

        static void ImportAll()
        {
            string[] files = Directory.GetFiles(ArtDir, "*.png");
            for (int i = 0; i < files.Length; i++)
            {
                string p = files[i].Replace('\\', '/');
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 100f;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
        }

        static void Save(string name, Color32[] px, int w, int h, float ppu)
        {
            if (name.StartsWith("prop_")) RimLight(new Color32(0xC8, 0xB4, 0x92, 255), 0.55f);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            File.WriteAllBytes(Path.Combine(ArtDir, name + ".png"), t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        /// <summary>最近邻缩放出图（用于关卡缩略图，保持像素硬边）。</summary>
        static void SaveScaled(string name, Color32[] src, int sw, int sh, int tw, int th)
        {
            Color32[] dst = new Color32[tw * th];
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    int sx = Mathf.Clamp(x * sw / tw, 0, sw - 1);
                    int sy = Mathf.Clamp(y * sh / th, 0, sh - 1);
                    dst[y * tw + x] = src[sy * sw + sx];
                }
            var t = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            t.SetPixels32(dst);
            t.Apply();
            File.WriteAllBytes(Path.Combine(ArtDir, name + ".png"), t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        /// <summary>给道具的上边缘加一圈受光，让它们能在暗背景上读出来。</summary>
        static void RimLight(Color32 rim, float strength)
        {
            Color32[] copy = (Color32[])_px.Clone();
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    int i = y * _w + x;
                    if (copy[i].a < 8) continue;
                    bool aboveEmpty = (y + 1 >= _h) || copy[i + _w].a < 8;
                    if (aboveEmpty) Blend(x, y, rim, strength);
                }
        }

        // ─────────────────────────────────────────────
        // 场景搭建
        // ─────────────────────────────────────────────

        public static void BuildScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var old = GameObject.Find("LevelArt");
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject("LevelArt");
            var r = root.transform;

            // 相机参数读取
            var cam = Camera.main;
            float viewH = cam != null ? cam.orthographicSize * 2f : 800f;
            float viewW = viewH * (cam != null ? cam.aspect : 16f / 9f);

            // ── 背景（运行时由 Level6BackdropFitter 按实际宽高比铺满） ──
            var bg = MakeSprite("BG_Wasteland", ArtDir + "/bg_wasteland.png", r, 1);
            var bgSrPre = bg.GetComponent<SpriteRenderer>();
            Vector2 bgSize = bgSrPre.sprite != null ? bgSrPre.sprite.bounds.size : new Vector2(6.4f, 3.6f);
            float bgScale = Mathf.Max(viewW / bgSize.x, viewH / bgSize.y);
            float bgWorldH = bgSize.y * bgScale;
            bg.transform.position = new Vector3(CamX, -90f + (0.5f - 0.45f) * bgWorldH, 1f);
            bg.transform.localScale = new Vector3(bgScale, bgScale, 1f);
            var bgSr = bg.GetComponent<SpriteRenderer>();
            bgSr.sortingOrder = -20;

            var fitter = bg.AddComponent<Level6BackdropFitter>();
            fitter.targetCamera = cam;
            fitter.roadWorldY = -90f;
            fitter.roadFractionFromBottom = 0.45f;

            // ── 中景道具（站在路后侧的土沿上，尺度：世界单位高度） ──
            var mid = new GameObject("MidProps").transform;
            mid.SetParent(r, false);

            Place(mid, "prop_deadtree", -352, -62, 150f, -12, 0.92f, true);
            Place(mid, "prop_deadtree", 664, -60, 128f, -12, 0.88f, false);
            Place(mid, "prop_deadtree", 962, -56, 165f, -12, 0.84f, true);
            Place(mid, "prop_pole", -196, -58, 210f, -13, 0.9f, false);
            Place(mid, "prop_pole", 452, -56, 198f, -13, 0.88f, false);
            Place(mid, "prop_pole", 812, -60, 188f, -13, 0.85f, true);
            Place(mid, "prop_billboard", -92, -58, 78f, -14, 0.85f, false);
            Place(mid, "prop_rock_l", 232, -64, 42f, -12, 0.9f, false);
            Place(mid, "prop_rock_l", 736, -66, 34f, -12, 0.86f, true);
            Place(mid, "prop_bush", -262, -68, 17f, -11, 0.95f, false);
            Place(mid, "prop_bush", 596, -70, 15f, -11, 0.95f, true);
            Place(mid, "prop_bush", 58, -66, 14f, -11, 0.95f, false);

            // ── 路边道具（贴着战斗线的前沿） ──
            var roadside = new GameObject("RoadsideProps").transform;
            roadside.SetParent(r, false);

            Place(roadside, "prop_barrel", 36, -128, 26f, -6, 1.0f, false);
            Place(roadside, "prop_barrel", 56, -126, 23f, -6, 0.95f, true);
            Place(roadside, "prop_barrel", 486, -130, 25f, -6, 0.95f, false);
            Place(roadside, "prop_tire", -64, -134, 18f, -6, 0.95f, false);
            Place(roadside, "prop_tire", 344, -128, 16f, -6, 0.95f, true);
            Place(roadside, "prop_tire", 906, -136, 19f, -6, 0.95f, false);
            Place(roadside, "prop_sign", 254, -126, 42f, -6, 1.0f, false);
            Place(roadside, "prop_sign", -142, -130, 36f, -6, 0.95f, true);
            Place(roadside, "prop_wreck", 158, -146, 38f, -6, 0.92f, false);
            Place(roadside, "prop_rock_s", 614, -124, 24f, -6, 0.95f, false);
            Place(roadside, "prop_rock_s", -308, -128, 21f, -6, 0.92f, true);
            Place(roadside, "prop_bush", 700, -126, 16f, -6, 0.95f, false);

            // ── 前景剪影（崖壁上，位于单位之前） ──
            var fg = new GameObject("ForegroundProps").transform;
            fg.SetParent(r, false);

            Place(fg, "prop_rock_l", -348, -300, 62f, 24, 0.66f, false);
            Place(fg, "prop_rock_l", 148, -350, 78f, 24, 0.60f, true);
            Place(fg, "prop_rock_s", 452, -270, 40f, 24, 0.64f, false);
            Place(fg, "prop_rock_s", 828, -330, 50f, 24, 0.58f, true);
            Place(fg, "prop_rebar", -204, -250, 15f, 25, 0.78f, false);
            Place(fg, "prop_rebar", 606, -232, 13f, 25, 0.78f, true);
            Place(fg, "prop_wreck", -34, -300, 52f, 23, 0.55f, false);

            // ── 尘埃粒子 ──
            BuildDust(r);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void BuildDust(Transform parent)
        {
            var go = new GameObject("DustMotes");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 16f);
            main.startSize = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startColor = new Color(0.85f, 0.72f, 0.55f, 0.16f);
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1400f, 500f, 1f);
            go.transform.position = new Vector3(300f, -100f, 2f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(3f, 9f);
            vel.y = new ParticleSystem.MinMaxCurve(-1f, 2f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sortingOrder = 40;
            r.material = new Material(Shader.Find("Sprites/Default"));
            r.renderMode = ParticleSystemRenderMode.Billboard;
        }

        static GameObject MakeSprite(string name, string path, Transform parent, float ppu)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null) { Debug.LogWarning("[Level6Art] missing sprite " + path); return null; }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.drawMode = SpriteDrawMode.Simple;
            return go;
        }

        static void Place(Transform parent, string texName, float x, float baseY, float worldHeight, int order, float shade, bool flip)
        {
            var go = MakeSprite(texName, ArtDir + "/" + texName + ".png", parent, 100f);
            if (go == null) return;
            var sr = go.GetComponent<SpriteRenderer>();
            float h0 = sr.sprite != null ? sr.sprite.bounds.size.y : 1f;
            float s = h0 > 0.0001f ? worldHeight / h0 : 1f;
            go.transform.localPosition = new Vector3(x, baseY + worldHeight * 0.5f, 0f);
            go.transform.localScale = new Vector3(flip ? -s : s, s, 1f);
            sr.sortingOrder = order;
            sr.color = new Color(shade, shade * 0.98f, shade * 0.95f, 1f);
        }

        // ─────────────────────────────────────────────
        // 像素工具
        // ─────────────────────────────────────────────

        static void Begin(int w, int h, int seed)
        {
            _w = w; _h = h; _px = new Color32[w * h];
            _rng = new System.Random(seed);
            for (int i = 0; i < _px.Length; i++) _px[i] = new Color32(0, 0, 0, 0);
        }

        static void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            _px[y * _w + x] = c;
        }

        static void Blend(int x, int y, Color32 c, float a)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            a = Mathf.Clamp01(a);
            Color32 d = _px[y * _w + x];
            if (d.a == 0) { _px[y * _w + x] = new Color32(c.r, c.g, c.b, (byte)(255 * a)); return; }
            _px[y * _w + x] = new Color32(
                (byte)Mathf.Lerp(d.r, c.r, a),
                (byte)Mathf.Lerp(d.g, c.g, a),
                (byte)Mathf.Lerp(d.b, c.b, a),
                (byte)Mathf.Max((int)d.a, (int)(255 * a)));
        }

        static bool Dither(int x, int y, float t)
        {
            if (t <= 0f) return false;
            if (t >= 1f) return true;
            return t > (Bayer4[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
        }

        static Color32 RampDither(Color32[] stops, float t, int x, int y)
        {
            if (stops.Length == 1) return stops[0];
            float f = Mathf.Clamp01(t) * (stops.Length - 1);
            int i = Mathf.FloorToInt(f);
            if (i >= stops.Length - 1) return stops[stops.Length - 1];
            return Dither(x, y, f - i) ? stops[i + 1] : stops[i];
        }

        static Color32 Darken(Color32 c, float k)
        {
            return new Color32((byte)(c.r * (1f - k)), (byte)(c.g * (1f - k)), (byte)(c.b * (1f - k)), c.a);
        }

        static Color32 Lighten(Color32 c, float k)
        {
            return new Color32(
                (byte)Mathf.Min(255, c.r + (255 - c.r) * k),
                (byte)Mathf.Min(255, c.g + (255 - c.g) * k),
                (byte)Mathf.Min(255, c.b + (255 - c.b) * k),
                c.a);
        }

        static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed);
            float c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
            float u = fx * fx * (3f - 2f * fx), v = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static float Hash(int x, int y, int seed)
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h = h ^ (h >> 16);
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }
}
