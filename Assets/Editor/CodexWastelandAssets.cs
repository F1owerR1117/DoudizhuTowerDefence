using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using DoudizhuTower.Config;

/// <summary>
/// 像素废土：字体资产生成 + Codex 数据修补。
/// 菜单 Tools/PixelWasteland/3、4
/// </summary>
public static class CodexWastelandAssets
{
    public const string FontDir = "Assets/Art/PixelWasteland/Fonts";
    public const string FontPath = FontDir + "/ArkPixel12 SDF.asset";
    public const string IconDir = "Assets/Art/PixelWasteland/Icons";

    // ---------------------------------------------------------------- 3. 字体
    [MenuItem("Tools/PixelWasteland/3 - Create Pixel Font Asset")]
    public static void CreatePixelFont()
    {
        string ttfPath = FontDir + "/ArkPixel12.ttf";
        Font src = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (src == null)
        {
            Debug.LogError("[PixelWasteland] 找不到字体源文件: " + ttfPath);
            return;
        }

        // RASTER = 位图采样，像素字体必须用它；SDF 会带来柔边。
        // padding 必须 > 0：留 0 时相邻字形的四边形会互相采样，出现"重影"。
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(
            src, 12, 2, GlyphRenderMode.RASTER, 2048, 2048,
            AtlasPopulationMode.Dynamic, true);

        if (fa == null)
        {
            Debug.LogError("[PixelWasteland] 字体资产创建失败");
            return;
        }

        fa.name = "ArkPixel12 SDF";
        if (fa.atlasTextures != null && fa.atlasTextures.Length > 0)
            fa.atlasTextures[0].filterMode = FilterMode.Point;

        // 先把工程会用到的字一次性灌入图集：图集定型后材质里的
        // _TextureWidth / _TextureHeight 才是正确值，否则 UV 算错会出现重影。
        string preload = BuildCharset();
        string missing;
        bool ok = fa.TryAddCharacters(preload, out missing);

        if (fa.atlasTextures != null && fa.atlasTextures.Length > 0)
        {
            int aw = fa.atlasTextures[0].width;
            int ah = fa.atlasTextures[0].height;
            if (fa.material != null)
            {
                fa.material.SetFloat(ShaderUtilities.ID_TextureWidth, aw);
                fa.material.SetFloat(ShaderUtilities.ID_TextureHeight, ah);
                fa.material.SetFloat(ShaderUtilities.ID_GradientScale, 1f);
            }
            Debug.Log(string.Format("[PixelWasteland] 预载 {0} 字，缺字 {1}，图集 {2}x{3}，shader={4}",
                preload.Length, (missing != null ? missing.Length : 0), aw, ah,
                fa.material != null ? fa.material.shader.name : "null"));
        }

        AssetDatabase.DeleteAsset(FontPath);
        AssetDatabase.CreateAsset(fa, FontPath);
        if (fa.atlasTextures != null && fa.atlasTextures.Length > 0)
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        if (fa.material != null)
            AssetDatabase.AddObjectToAsset(fa.material, fa);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[PixelWasteland] 像素字体已生成: " + FontPath + " ok=" + ok);
    }

    /// <summary>收集工程里会用到的字符：ASCII + 界面文案 + 所有图鉴条目正文</summary>
    private static string BuildCharset()
    {
        HashSet<char> set = new HashSet<char>();
        for (int c = 32; c <= 126; c++) set.Add((char)c);

        string ui = "解锁进度已未返回菜单数据库检索档案关联标题名称点数双王建筑被动规则废土档案终端" +
                    "条目编号分类状态数据确认搜索结果没有匹配的档案试试点击查看完整内容" +
                    "…·—「」『』《》〈〉【】、。，；：？！×÷°█▸◀◆■←→↑↓";
        foreach (char c in ui) set.Add(c);

        string[] guids = AssetDatabase.FindAssets("t:CodexEntry", new[] { "Assets/Scripts/UI/Codex" });
        foreach (string g in guids)
        {
            CodexEntry e = AssetDatabase.LoadAssetAtPath<CodexEntry>(AssetDatabase.GUIDToAssetPath(g));
            if (e == null) continue;
            AddAll(set, e.DisplayName);
            AddAll(set, e.Description);
            AddAll(set, e.ExtraInfo);
            if (e.Keywords != null)
                foreach (string k in e.Keywords) AddAll(set, k);
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder(set.Count);
        foreach (char c in set) sb.Append(c);
        return sb.ToString();
    }

    private static void AddAll(HashSet<char> set, string s)
    {
        if (string.IsNullOrEmpty(s)) return;
        foreach (char c in s) set.Add(c);
    }

    // ---------------------------------------------------------------- 4. 数据
    private static readonly Dictionary<string, string> IdMap = new Dictionary<string, string>
    {
        // 牌型
        { "单张", "cardtype-single" }, { "对子", "cardtype-pair" },
        { "三张", "cardtype-triple" }, { "三带一", "cardtype-triple-one" },
        { "三带二", "cardtype-triple-pair" }, { "顺子", "cardtype-straight" },
        { "连对", "cardtype-consecutive-pair" }, { "飞机", "cardtype-plane" },
        { "四带二", "cardtype-four-two" }, { "炸弹", "cardtype-bomb" },
        { "王炸", "cardtype-kingbomb" },
        // 数值
        { "3", "cardvalue-3" }, { "4", "cardvalue-4" }, { "5", "cardvalue-5" },
        { "6", "cardvalue-6" }, { "7", "cardvalue-7" }, { "8", "cardvalue-8" },
        { "9", "cardvalue-9" }, { "10", "cardvalue-10" }, { "J", "cardvalue-j" },
        { "Q", "cardvalue-q" }, { "K", "cardvalue-k" }, { "A", "cardvalue-a" },
        { "2", "cardvalue-2" }, { "Joker", "cardvalue-joker" },
        // BOSS / 规则
        { "王国骑士", "boss-knight" },
        { "反制护盾", "rule-shield" }, { "要不起领域", "rule-pass-domain" },
    };

    private static string IconFor(CodexCategory cat)
    {
        switch (cat)
        {
            case CodexCategory.CardValue: return "card";
            case CodexCategory.CardType: return "ladder";
            case CodexCategory.Boss: return "skull";
            case CodexCategory.Building: return "tower";
            case CodexCategory.Passive: return "chip";
            default: return "doc";
        }
    }

    [MenuItem("Tools/PixelWasteland/4 - Fix Codex Data")]
    public static void FixCodexData()
    {
        string[] guids = AssetDatabase.FindAssets("t:CodexEntry", new[] { "Assets/Scripts/UI/Codex" });
        int fixedIds = 0, fixedIcons = 0, total = 0;

        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            CodexEntry e = AssetDatabase.LoadAssetAtPath<CodexEntry>(path);
            if (e == null) continue;
            total++;

            if (string.IsNullOrEmpty(e.Id))
            {
                string key = Path.GetFileNameWithoutExtension(path);
                string id;
                if (!IdMap.TryGetValue(key, out id))
                    id = e.Category.ToString().ToLower() + "-" + total;
                e.Id = id;
                fixedIds++;
            }

            bool needIcon = e.Icon == null;
            if (!needIcon)
            {
                string iconPath = AssetDatabase.GetAssetPath(e.Icon);
                needIcon = string.IsNullOrEmpty(iconPath) || !iconPath.StartsWith("Assets/Art/PixelWasteland/");
            }
            if (needIcon)
            {
                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(
                    IconDir + "/" + IconFor(e.Category) + ".png");
                if (icon != null)
                {
                    e.Icon = icon;
                    fixedIcons++;
                }
            }

            EditorUtility.SetDirty(e);
        }

        // 补两个缺失的数据库
        EnsureDatabase("Building", CodexCategory.Building);
        EnsureDatabase("Passive", CodexCategory.Passive);

        AssetDatabase.SaveAssets();
        Debug.Log(string.Format("[PixelWasteland] 条目 {0} 个，补 Id {1} 个，补图标 {2} 个", total, fixedIds, fixedIcons));
    }

    private static void EnsureDatabase(string name, CodexCategory cat)
    {
        string dir = "Assets/Scripts/UI/Codex/" + name;
        string path = dir + "/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<CodexDatabase>(path) != null) return;

        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Scripts/UI/Codex", name);

        CodexDatabase db = ScriptableObject.CreateInstance<CodexDatabase>();
        db.Entries = new List<CodexEntry>();
        AssetDatabase.CreateAsset(db, path);
        Debug.Log("[PixelWasteland] 已创建空数据库: " + path);
    }
}
