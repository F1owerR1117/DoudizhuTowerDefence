using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using DoudizhuTower.Config;
using DoudizhuTower.UI.Codex;

/// <summary>
/// 像素废土：Codex 场景复刻 HTML 预览（v3）。
/// 菜单 Tools/PixelWasteland/5、6
/// </summary>
public static class CodexWastelandBuilder
{
    const string ArtRoot = "Assets/Art/PixelWasteland/";
    const string FontPath = ArtRoot + "Fonts/ArkPixel12 SDF.asset";
    const string ScenePath = "Assets/Scenes/Codex.unity";

    // ── 参考 HTML：header 96 / hazard 8 / tabs 124~182 / content 202 / 面板 700 ──
    const float PAD = 40f;
    const float HEADER_H = 92f;
    const float HAZARD_H = 8f;
    const float TABS_TOP = 114f;
    const float TABS_H = 58f;
    const float CONTENT_TOP = 190f;
    const float CONTENT_BOTTOM = 60f;
    const float LEFT_W = 700f;
    const float GAP = 40f;
    const float PANEL_H = 700f;   // 与预览一致：面板不到底，下方留给状态条

    // 字号：保留放大后的基准（标题 48 / 正文 24 / 标签 18）
    const float FS_TITLE = 48f;
    const float FS_BODY = 24f;
    const float FS_LABEL = 18f;
    const float FS_META = 12f;
    const float FS_CHIP = 18f;

    const float ROW_H = 74f;
    const float ROW_ICON = 40f;

    static readonly Color Bone = Hex("#E9DCC0");
    static readonly Color BoneDim = Hex("#AB9B80");
    static readonly Color BoneDark = Hex("#776853");
    static readonly Color Amber = Hex("#F0A050");
    static readonly Color Toxic = Hex("#A8EE54");
    static readonly Color Blood = Hex("#C04A38");
    static readonly Color Phosphor = Hex("#6ADFA6");
    static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);

    // ------------------------------------------------------------------ 工具
    static Color Hex(string s) { Color c; ColorUtility.TryParseHtmlString(s, out c); return c; }

    static Sprite S(string name)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + name + ".png");
        if (s == null) Debug.LogWarning("[Wasteland] 缺少贴图: " + name);
        return s;
    }

    static TMP_FontAsset Font()
    {
        TMP_FontAsset f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (f == null) Debug.LogError("[Wasteland] 缺少像素字体: " + FontPath);
        return f;
    }

    static Transform Find(Transform root, string path)
    {
        Transform t = root.Find(path);
        if (t == null) Debug.LogWarning("[Wasteland] 找不到节点: " + path);
        return t;
    }

    static Transform GetOrCreate(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t;
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static RectTransform RT(Transform t) { return t as RectTransform; }

    static void Stretch(Transform t)
    {
        RectTransform rt = RT(t);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Place(Transform t, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform rt = RT(t);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void StretchTo(Transform t, float left, float bottom, float right, float top)
    {
        RectTransform rt = RT(t);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    static Image Img(Transform t)
    {
        Image i = t.GetComponent<Image>();
        if (i == null) i = t.gameObject.AddComponent<Image>();
        return i;
    }

    static void SetSliced(Image i, string sprite, bool raycast)
    {
        i.sprite = S(sprite);
        i.type = Image.Type.Sliced;
        i.color = Color.white;
        i.pixelsPerUnitMultiplier = 1f;
        i.raycastTarget = raycast;
    }

    static TextMeshProUGUI Text(Transform t)
    {
        TextMeshProUGUI x = t.GetComponent<TextMeshProUGUI>();
        if (x == null) x = t.gameObject.AddComponent<TextMeshProUGUI>();
        return x;
    }

    static void Style(TextMeshProUGUI t, string content, float size, Color color, TextAlignmentOptions align)
    {
        t.font = Font();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.fontStyle = FontStyles.Normal;
        t.extraPadding = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
    }

    static TextMeshProUGUI NewText(Transform parent, string name, string content, float size,
        Color color, TextAlignmentOptions align)
    {
        Transform t = GetOrCreate(parent, name);
        TextMeshProUGUI x = Text(t);
        Style(x, content, size, color, align);
        return x;
    }

    static Image NewImage(Transform parent, string name, string sprite, Vector2 size)
    {
        Transform t = GetOrCreate(parent, name);
        Image i = Img(t);
        i.sprite = S(sprite);
        i.type = Image.Type.Simple;
        i.preserveAspect = true;
        i.raycastTarget = false;
        RT(t).sizeDelta = size;
        return i;
    }

    static void AddPanelShadow(Transform parent, Transform panel, string name)
    {
        RectTransform prt = RT(panel);
        Transform sh = GetOrCreate(parent, name);
        sh.SetAsLastSibling();
        sh.SetSiblingIndex(panel.GetSiblingIndex());

        Image img = Img(sh);
        img.sprite = S("UI/panel_large");
        img.type = Image.Type.Sliced;
        img.color = new Color(0f, 0f, 0f, 0.5f);
        img.raycastTarget = false;

        RectTransform srt = RT(sh);
        srt.anchorMin = prt.anchorMin;
        srt.anchorMax = prt.anchorMax;
        srt.pivot = prt.pivot;
        srt.sizeDelta = prt.sizeDelta;
        srt.anchoredPosition = prt.anchoredPosition + new Vector2(12f, -12f);
    }

    static Scrollbar BuildScrollbar(Transform parent, string name, string trackSprite, string thumbSprite, float width)
    {
        Transform sb = GetOrCreate(parent, name);
        Image sbi = Img(sb);
        SetSliced(sbi, trackSprite, true);
        sbi.color = new Color(0.6f, 0.6f, 0.6f, 1f);

        RectTransform sbrt = RT(sb);
        sbrt.anchorMin = new Vector2(1f, 0f);
        sbrt.anchorMax = new Vector2(1f, 1f);
        sbrt.pivot = new Vector2(1f, 0.5f);
        sbrt.anchoredPosition = Vector2.zero;
        sbrt.sizeDelta = new Vector2(width, 0f);

        Scrollbar bar = sb.GetComponent<Scrollbar>();
        if (bar == null) bar = sb.gameObject.AddComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;

        Transform area = GetOrCreate(sb, "Sliding Area");
        StretchTo(area, 2f, 2f, -2f, -2f);

        Transform handle = GetOrCreate(area, "Handle");
        Stretch(handle);
        Image hi = Img(handle);
        SetSliced(hi, thumbSprite, true);

        bar.handleRect = RT(handle);
        bar.targetGraphic = hi;
        bar.size = 0.3f;
        return bar;
    }

    static RawImage Overlay(Transform root, string name, string spriteName, float alpha)
    {
        Transform t = GetOrCreate(root, name);
        RawImage r = t.GetComponent<RawImage>();
        if (r == null) r = t.gameObject.AddComponent<RawImage>();
        Sprite sp = S(spriteName);
        r.texture = sp != null ? sp.texture : null;
        r.color = new Color(1f, 1f, 1f, alpha);
        r.raycastTarget = false;
        Stretch(t);
        return r;
    }

    // ------------------------------------------------------------------ 5. 场景
    [MenuItem("Tools/PixelWasteland/5 - Restyle Codex Scene")]
    public static void Restyle()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[Wasteland] 场景里没有 Canvas"); return; }
        Transform root = canvas.rootCanvas.transform;

        // ── 背景 ──
        Transform bg = Find(root, "BackGround");
        if (bg != null)
        {
            Image bgi = Img(bg);
            bgi.sprite = S("BG/codex_bg");
            bgi.type = Image.Type.Simple;
            bgi.preserveAspect = false;
            bgi.color = Color.white;
            bgi.raycastTarget = false;
            Stretch(bg);
            bg.SetAsFirstSibling();
        }

        // ── 顶栏 ──
        Transform topBar = Find(root, "TopBar");
        if (topBar != null)
        {
            RectTransform tbrt = RT(topBar);
            tbrt.anchorMin = new Vector2(0f, 1f);
            tbrt.anchorMax = new Vector2(1f, 1f);
            tbrt.pivot = new Vector2(0.5f, 1f);
            tbrt.anchoredPosition = Vector2.zero;
            tbrt.sizeDelta = new Vector2(0f, HEADER_H);

            Image tbi = topBar.GetComponent<Image>();
            if (tbi == null) tbi = topBar.gameObject.AddComponent<Image>();
            tbi.sprite = null;
            tbi.color = new Color(0.04f, 0.03f, 0.025f, 0.55f);
            tbi.raycastTarget = false;

            Transform hz = GetOrCreate(topBar, "HazardLine");
            Image hzi = Img(hz);
            hzi.sprite = S("UI/hazard_strip");
            hzi.type = Image.Type.Tiled;
            hzi.raycastTarget = false;
            RectTransform hzrt = RT(hz);
            hzrt.anchorMin = new Vector2(0f, 0f);
            hzrt.anchorMax = new Vector2(1f, 0f);
            hzrt.pivot = new Vector2(0.5f, 0f);
            hzrt.anchoredPosition = Vector2.zero;
            hzrt.sizeDelta = new Vector2(0f, HAZARD_H);

            // 返回按钮（HTML: 200×52）
            Transform back = GetOrCreate(topBar, "BackButton");
            SetSliced(Img(back), "UI/btn_normal", true);
            Place(back, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(PAD, -22f), new Vector2(200f, 52f));

            Image arrow = NewImage(back, "Arrow", "Icons/arrow_left", new Vector2(28f, 28f));
            Place(arrow.transform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(30f, 0f), new Vector2(28f, 28f));

            Transform backTxtT = GetOrCreate(back, "Text (TMP)");
            TextMeshProUGUI backTxt = Text(backTxtT);
            Style(backTxt, "返回菜单", FS_BODY, Bone, TextAlignmentOptions.Center);
            RectTransform btrt = RT(backTxtT);
            btrt.anchorMin = Vector2.zero;
            btrt.anchorMax = Vector2.one;
            btrt.offsetMin = new Vector2(48f, 0f);
            btrt.offsetMax = new Vector2(-8f, 0f);

            // 标题
            Transform brand = GetOrCreate(topBar, "Brand");
            Place(brand, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(264f, -16f), new Vector2(760f, 64f));
            TextMeshProUGUI bt = NewText(brand, "Title", "废土档案终端", 36f, Bone, TextAlignmentOptions.TopLeft);
            Place(bt.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(760f, 42f));
            TextMeshProUGUI bs = NewText(brand, "Sub", "WASTELAND ARCHIVE TERMINAL · SECTOR 07",
                FS_META, BoneDark, TextAlignmentOptions.TopLeft);
            Place(bs.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -44f), new Vector2(760f, 16f));

            // 进度（HTML：标签 12 + 28 段方块条）
            Transform prog = GetOrCreate(topBar, "Progress");
            Place(prog, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-PAD, -16f), new Vector2(320f, 56f));
            TextMeshProUGUI pt = NewText(prog, "ProgressText", "已解档 0 / 0", FS_META, BoneDim,
                TextAlignmentOptions.BottomLeft);
            Place(pt.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(200f, 18f));

            Transform barBg = GetOrCreate(prog, "BarBg");
            Image bbg = Img(barBg);
            SetSliced(bbg, "UI/scroll_track", false);
            bbg.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            Place(barBg, new Vector2(0f, 0f), new Vector2(0f, 0f),
                Vector2.zero, new Vector2(320f, 14f));

            Transform barFill = GetOrCreate(prog, "BarFill");
            Image bf = Img(barFill);
            bf.sprite = S("UI/scroll_thumb");
            bf.type = Image.Type.Filled;
            bf.fillMethod = Image.FillMethod.Horizontal;
            bf.fillOrigin = 0;
            bf.fillAmount = 0f;
            bf.raycastTarget = false;
            bf.color = Amber;
            Place(barFill, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(2f, 2f), new Vector2(316f, 10f));
        }

        // ── 页签条 ──
        Transform tabs = Find(root, "TopBar/CategoryTabs");
        if (tabs == null) tabs = Find(root, "CategoryTabs");
        if (tabs != null)
        {
            tabs.SetParent(root, false);
            RectTransform trt = RT(tabs);
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(PAD, 0f);
            trt.offsetMax = new Vector2(-PAD, 0f);
            trt.sizeDelta = new Vector2(trt.sizeDelta.x, TABS_H);
            trt.anchoredPosition = new Vector2(0f, -TABS_TOP);

            HorizontalLayoutGroup hlg = tabs.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
        }

        // ── 主体：面板固定高度、垂直居中，间距靠统一节奏控制 ──
        Transform main = Find(root, "MainArea");
        if (main != null)
        {
            HorizontalLayoutGroup mhlg = main.GetComponent<HorizontalLayoutGroup>();
            if (mhlg != null) Object.DestroyImmediate(mhlg);
            StretchTo(main, PAD, CONTENT_BOTTOM, -PAD, -CONTENT_TOP);

            BuildLeftPanel(main);
            BuildRightPanel(main);
        }

        // ── 覆盖层：扫描线 / 扫光 / 暗角 / 玻璃 ──
        Transform scan = GetOrCreate(root, "Scanline");
        RawImage scanRaw = scan.GetComponent<RawImage>();
        if (scanRaw == null) scanRaw = scan.gameObject.AddComponent<RawImage>();
        Sprite scanSprite = S("UI/scanline");
        scanRaw.texture = scanSprite != null ? scanSprite.texture : null;
        scanRaw.uvRect = new Rect(0f, 0f, 480f, 270f);
        scanRaw.color = new Color(1f, 1f, 1f, 0.26f);
        scanRaw.raycastTarget = false;
        Stretch(scan);

        // ── 底部状态条（与预览一致）──
        Transform stat = GetOrCreate(root, "StatusBar");
        RectTransform stRt = RT(stat);
        stRt.anchorMin = new Vector2(0f, 0f);
        stRt.anchorMax = new Vector2(1f, 0f);
        stRt.pivot = new Vector2(0.5f, 0f);
        stRt.offsetMin = new Vector2(40f, 12f);
        stRt.offsetMax = new Vector2(-40f, 56f);
        Image statBg = Img(stat);
        statBg.sprite = null;
        statBg.color = Transparent;
        statBg.raycastTarget = false;

        TextMeshProUGUI sLive = NewText(stat, "Live", "● SYNC 在线", FS_META, Phosphor, TextAlignmentOptions.Left);
        Place(sLive.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(160f, 18f));
        TextMeshProUGUI sLink = NewText(stat, "Link", "LINK STABLE", FS_META, BoneDark, TextAlignmentOptions.Left);
        Place(sLink.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(170f, 0f), new Vector2(160f, 18f));
        TextMeshProUGUI sCache = NewText(stat, "Cache", "缓存 0 条", FS_META, BoneDark, TextAlignmentOptions.Left);
        Place(sCache.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(330f, 0f), new Vector2(140f, 18f));
        TextMeshProUGUI sFps = NewText(stat, "Fps", "60 FPS", FS_META, BoneDark, TextAlignmentOptions.Left);
        Place(sFps.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(470f, 0f), new Vector2(120f, 18f));
        TextMeshProUGUI sBuild = NewText(stat, "Build", "PIXEL WASTELAND · BUILD 0.9.3 · 1920×1080", FS_META, BoneDark, TextAlignmentOptions.Right);
        Place(sBuild.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(520f, 18f));

        // 玻璃 / 扫光：垫在 UI 之下，只照亮场景，面板保持纯黑（方案 A）
        RawImage sweep = Overlay(root, "Sweep", "UI/sweep", 0.55f);
        UISweep sweepComp = sweep.GetComponent<UISweep>();
        if (sweepComp == null) sweepComp = sweep.gameObject.AddComponent<UISweep>();
        sweepComp.cycle = 9f;
        sweepComp.travel = 1300f;

        RawImage glass = Overlay(root, "Glass", "UI/glass", 0.9f);

        // 扫描线 / 暗角：仍在最上层（会在 UI 上压出 CRT 条纹与压暗四角）
        RawImage vig = Overlay(root, "Vignette", "UI/vignette", 0.9f);

        if (bg != null) bg.SetSiblingIndex(0);
        sweep.transform.SetSiblingIndex(1);
        glass.transform.SetSiblingIndex(2);
        scan.SetAsLastSibling();
        vig.transform.SetAsLastSibling();

        // ── 统一字体 ──
        TMP_FontAsset font = Font();
        if (font != null)
        {
            TextMeshProUGUI[] all = Object.FindObjectsByType<TextMeshProUGUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TextMeshProUGUI t in all)
                if (t.font != font) t.font = font;
        }

        BindAll(root);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PixelWasteland] Codex 场景复刻完成（v3）");
    }

    // ------------------------------------------------------------------ 左面板
    static void BuildLeftPanel(Transform main)
    {
        Transform left = Find(main, "LeftPanel");
        if (left == null) return;

        SetSliced(Img(left), "UI/panel_large", false);
        RectTransform lrt = RT(left);
        lrt.anchorMin = new Vector2(0f, 1f);
        lrt.anchorMax = new Vector2(0f, 1f);
        lrt.pivot = new Vector2(0f, 1f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(LEFT_W, PANEL_H);
        AddPanelShadow(main, left, "LeftPanelShadow");

        // 用从 HTML 直接烘焙的面板贴图（1:1 尺寸，不拉伸）
        Image lbg = Img(left);
        Sprite bakedLeft = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "Baked/panel_left.png");
        if (bakedLeft != null)
        {
            lbg.sprite = bakedLeft;
            lbg.type = Image.Type.Simple;
            lbg.preserveAspect = false;
            lbg.color = Color.white;
        }
        else SetSliced(lbg, "UI/panel_large", false);

        // 头部：标题 + 数量徽章
        TextMeshProUGUI hd = NewText(left, "HeaderText",
            "数据库  <size=60%><color=#776853>DATABASE</color></size>",
            FS_BODY, Bone, TextAlignmentOptions.Left);
        Place(hd.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -20f), new Vector2(420f, 32f));

        Transform badge = GetOrCreate(left, "CountBadge");
        SetSliced(Img(badge), "UI/panel_inset", false);
        Place(badge, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-24f, -20f), new Vector2(46f, 30f));
        TextMeshProUGUI badgeT = NewText(badge, "Label", "00", FS_META, Toxic, TextAlignmentOptions.Center);
        Stretch(badgeT.transform);

        // 搜索
        Transform search = Find(left, "SearchInput");
        if (search != null)
        {
            SetSliced(Img(search), "UI/panel_inset", true);
            Place(search, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -62f), new Vector2(652f, 56f));

            TMP_InputField input = search.GetComponent<TMP_InputField>();
            if (input != null)
            {
                input.text = string.Empty;
                Transform textArea = search.Find("Text Area");
                if (textArea != null)
                {
                    StretchTo(textArea, 16f, 8f, -16f, -8f);
                    Transform ph = textArea.Find("Placeholder");
                    if (ph != null)
                    {
                        TextMeshProUGUI p = Text(ph);
                        Style(p, "输入名称 / 关键词 检索档案…", FS_BODY,
                            new Color(0.42f, 0.87f, 0.65f, 0.7f), TextAlignmentOptions.Left);
                        Stretch(ph);
                    }
                    Transform tx = textArea.Find("Text");
                    if (tx != null)
                    {
                        TextMeshProUGUI x = Text(tx);
                        Style(x, "", FS_BODY, Phosphor, TextAlignmentOptions.Left);
                        Stretch(tx);
                    }
                }
            }
        }

        // 列表
        Transform sv = Find(left, "Scroll View");
        if (sv != null)
        {
            Image svi = Img(sv);
            svi.sprite = null;
            svi.color = Transparent;
            svi.raycastTarget = true;
            StretchTo(sv, 24f, 44f, -24f, -130f);

            Transform vp = sv.Find("Viewport");
            if (vp != null)
            {
                SetSliced(Img(vp), "UI/panel_inset", false);
                StretchTo(vp, 0f, 0f, -18f, 0f);
            }

            Transform content = sv.Find("Viewport/Content");
            if (content != null)
            {
                RectTransform crt = RT(content);
                crt.anchorMin = new Vector2(0f, 1f);
                crt.anchorMax = new Vector2(1f, 1f);
                crt.pivot = new Vector2(0.5f, 1f);
                crt.anchoredPosition = Vector2.zero;
                crt.sizeDelta = new Vector2(0f, ROW_H);

                VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
                if (vlg == null) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 2f;
                vlg.padding = new RectOffset(8, 8, 8, 8);
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
                if (csf == null) csf = content.gameObject.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            Transform sb = sv.Find("Scrollbar Vertical");
            if (sb != null)
            {
                SetSliced(Img(sb), "UI/scroll_track", true);
                RectTransform sbrt = RT(sb);
                sbrt.anchorMin = new Vector2(1f, 0f);
                sbrt.anchorMax = new Vector2(1f, 1f);
                sbrt.pivot = new Vector2(1f, 0.5f);
                sbrt.offsetMin = new Vector2(-16f, 4f);
                sbrt.offsetMax = new Vector2(-2f, -4f);
                Transform handle = sb.Find("Sliding Area/Handle");
                if (handle != null) SetSliced(Img(handle), "UI/scroll_thumb", true);
            }
        }

        // 底部栏
        Transform footer = GetOrCreate(left, "Footer");
        Place(footer, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(24f, 14f), new Vector2(652f, 26f));
        TextMeshProUGUI fc = NewText(footer, "Count", "共 0 条 / 全部 0 条", FS_META, BoneDark,
            TextAlignmentOptions.Left);
        Stretch(fc.transform);
        TextMeshProUGUI fh = NewText(footer, "Hint", "↑↓ 选择 · ←→ 分类", FS_META, Hex("#574A3F"),
            TextAlignmentOptions.Right);
        Stretch(fh.transform);
    }

    // ------------------------------------------------------------------ 右面板
    static void BuildRightPanel(Transform main)
    {
        Transform right = Find(main, "RightPanel");
        if (right == null) return;

        RectTransform rrt = RT(right);
        rrt.anchorMin = new Vector2(0f, 1f);
        rrt.anchorMax = new Vector2(1f, 1f);
        rrt.pivot = new Vector2(0.5f, 1f);
        rrt.offsetMin = new Vector2(LEFT_W + GAP, -PANEL_H);
        rrt.offsetMax = new Vector2(0f, 0f);
        AddPanelShadow(main, right, "RightPanelShadow");

        Image rbg = Img(right);
        Sprite bakedRight = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "Baked/panel_right.png");
        if (bakedRight != null)
        {
            rbg.sprite = bakedRight;
            rbg.type = Image.Type.Simple;
            rbg.preserveAspect = false;
            rbg.color = Color.white;
        }
        else SetSliced(rbg, "UI/panel_large", false);

        // ── 芯片行 ──
        Transform chips = GetOrCreate(right, "Chips");
        Place(chips, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(26f, -18f), new Vector2(700f, 32f));

        Transform catChip = GetOrCreate(chips, "CategoryChip");
        SetSliced(Img(catChip), "UI/panel_inset", false);
        Place(catChip, new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(200f, 32f));
        Image catIcon = NewImage(catChip, "Icon", "Icons/card", new Vector2(22f, 22f));
        Place(catIcon.transform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(20f, 0f), new Vector2(22f, 22f));
        TextMeshProUGUI catT = NewText(catChip, "Label", "卡牌数值", FS_CHIP, Amber, TextAlignmentOptions.Left);
        StretchTo(catT.transform, 38f, 0f, -10f, 0f);

        Transform idChip = GetOrCreate(chips, "IdChip");
        SetSliced(Img(idChip), "UI/panel_inset", false);
        Place(idChip, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(212f, 0f), new Vector2(190f, 32f));
        TextMeshProUGUI idT = NewText(idChip, "Label", "—", FS_CHIP, BoneDim, TextAlignmentOptions.Center);
        Stretch(idT.transform);

        // 印章
        Transform stamp = GetOrCreate(right, "Stamp");
        SetSliced(Img(stamp), "UI/btn_normal", false);
        Place(stamp, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-26f, -14f), new Vector2(150f, 38f));
        stamp.localEulerAngles = new Vector3(0f, 0f, 4f);
        TextMeshProUGUI stampT = NewText(stamp, "Text", "未解档", FS_LABEL, Blood, TextAlignmentOptions.Center);
        Stretch(stampT.transform);

        // ── 标题 / 强调条 / 副标题 ──
        Transform accent = GetOrCreate(right, "TitleAccent");
        Image ai = Img(accent);
        ai.color = Amber;
        ai.raycastTarget = false;
        Place(accent, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(14f, -54f), new Vector2(5f, 56f));

        TextMeshProUGUI title = NewText(right, "TitleText", "选择档案", FS_TITLE, Bone,
            TextAlignmentOptions.TopLeft);
        RectTransform trt2 = RT(title.transform);
        trt2.anchorMin = new Vector2(0f, 1f);
        trt2.anchorMax = new Vector2(1f, 1f);
        trt2.pivot = new Vector2(0f, 1f);
        trt2.offsetMin = new Vector2(36f, -108f);
        trt2.offsetMax = new Vector2(-26f, -56f);

        TextMeshProUGUI sub = NewText(right, "SubText", "", FS_META, BoneDim,
            TextAlignmentOptions.TopLeft);
        RectTransform srt2 = RT(sub.transform);
        srt2.anchorMin = new Vector2(0f, 1f);
        srt2.anchorMax = new Vector2(1f, 1f);
        srt2.pivot = new Vector2(0f, 1f);
        srt2.offsetMin = new Vector2(36f, -130f);
        srt2.offsetMax = new Vector2(-26f, -110f);

        // ── 分隔线（两端方块端帽）──
        Transform rule = GetOrCreate(right, "RuleLine");
        Image ri = Img(rule);
        ri.color = Hex("#574A3F");
        ri.raycastTarget = false;
        RectTransform rrt2 = RT(rule);
        rrt2.anchorMin = new Vector2(0f, 1f);
        rrt2.anchorMax = new Vector2(1f, 1f);
        rrt2.pivot = new Vector2(0.5f, 1f);
        rrt2.offsetMin = new Vector2(26f, -142f);
        rrt2.offsetMax = new Vector2(-26f, -138f);
        for (int i = 0; i < 2; i++)
        {
            Transform cap = GetOrCreate(rule, i == 0 ? "CapL" : "CapR");
            Image ci = Img(cap);
            ci.color = Hex("#726151");
            ci.raycastTarget = false;
            Place(cap, new Vector2(i == 0 ? 0f : 1f, 0.5f), new Vector2(i == 0 ? 0f : 1f, 0.5f),
                Vector2.zero, new Vector2(10f, 10f));
        }

        // ── 图标插槽：棋盘底 + 外框 + 图标 ──
        Transform iconT = Find(right, "IconImage");
        if (iconT != null)
        {
            Place(iconT, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(26f, -152f), new Vector2(240f, 240f));
            Image ii = Img(iconT);
            ii.sprite = null;
            ii.color = Transparent;
            ii.raycastTarget = false;

            Transform checker = GetOrCreate(iconT, "Checker");
            Image chk = Img(checker);
            chk.sprite = S("UI/socket_checker");
            chk.type = Image.Type.Tiled;
            chk.raycastTarget = false;
            StretchTo(checker, 10f, 10f, -10f, -10f);

            Transform frame = GetOrCreate(iconT, "Frame");
            SetSliced(Img(frame), "UI/socket_frame", false);
            Stretch(frame);

            Image glyph = NewImage(iconT, "Glyph", "Icons/card", new Vector2(176f, 176f));
            Place(glyph.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(176f, 176f));

            // 显式排序：棋盘在最下、外框居中、图标最上（否则后建节点会盖住图标）
            checker.SetSiblingIndex(0);
            frame.SetSiblingIndex(1);
            glyph.transform.SetSiblingIndex(2);
        }

        // ── 描述滚动区 ──
        Transform descScroll = GetOrCreate(right, "DescScroll");
        ScrollRect dsr = descScroll.GetComponent<ScrollRect>();
        if (dsr == null) dsr = descScroll.gameObject.AddComponent<ScrollRect>();
        StretchTo(descScroll, 298f, 300f, -26f, -152f);
        // 描述区固定高度 240，其余区块自上而下紧凑排列，避免出现大片空屏
        RectTransform dsrt = RT(descScroll);
        dsrt.anchorMin = new Vector2(0f, 1f);
        dsrt.anchorMax = new Vector2(1f, 1f);
        dsrt.pivot = new Vector2(0.5f, 1f);
        dsrt.offsetMin = new Vector2(250f, -392f);
        dsrt.offsetMax = new Vector2(-26f, -152f);

        Transform dView = GetOrCreate(descScroll, "Viewport");
        StretchTo(dView, 0f, 0f, -18f, 0f);
        Image dvi = Img(dView);
        dvi.sprite = S("UI/panel_inset");
        dvi.type = Image.Type.Sliced;
        dvi.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        dvi.raycastTarget = true;
        if (dView.GetComponent<RectMask2D>() == null) dView.gameObject.AddComponent<RectMask2D>();

        Transform descT = dView.Find("DescirptionText");
        Transform oldDesc = right.Find("DescirptionText");
        if (descT == null && oldDesc != null) { oldDesc.SetParent(dView, false); descT = oldDesc; }
        else if (descT != null && oldDesc != null) { Object.DestroyImmediate(oldDesc.gameObject); }
        if (descT == null) descT = GetOrCreate(dView, "DescirptionText");

        TextMeshProUGUI desc = Text(descT);
        Style(desc, "从左侧列表选择一条记录，查看它的详细说明与关联档案。", FS_BODY, Bone,
            TextAlignmentOptions.TopLeft);
        desc.lineSpacing = 12f;
        RectTransform drt = RT(descT);
        drt.anchorMin = new Vector2(0f, 1f);
        drt.anchorMax = new Vector2(1f, 1f);
        drt.pivot = new Vector2(0.5f, 1f);
        drt.anchoredPosition = new Vector2(0f, -14f);
        drt.sizeDelta = new Vector2(-32f, 100f);
        ContentSizeFitter dcsf = descT.GetComponent<ContentSizeFitter>();
        if (dcsf == null) dcsf = descT.gameObject.AddComponent<ContentSizeFitter>();
        dcsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        dcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar descBar = BuildScrollbar(descScroll, "Scrollbar Vertical",
            "UI/scroll_track", "UI/scroll_thumb", 14f);
        dsr.content = drt;
        dsr.viewport = RT(dView);
        dsr.horizontal = false;
        dsr.vertical = true;
        dsr.movementType = ScrollRect.MovementType.Clamped;
        dsr.scrollSensitivity = 28f;
        dsr.verticalScrollbar = descBar;
        dsr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        ScrollbarAutoHide ah = descScroll.GetComponent<ScrollbarAutoHide>();
        if (ah == null) ah = descScroll.gameObject.AddComponent<ScrollbarAutoHide>();
        ah.bar = descBar;

        // ── 数据块 ──
        Transform extra = Find(right, "ExtraInfoContainer");
        if (extra != null)
        {
            RectTransform ert = RT(extra);
            ert.anchorMin = new Vector2(0f, 1f);
            ert.anchorMax = new Vector2(1f, 1f);
            ert.pivot = new Vector2(0.5f, 1f);
            ert.offsetMin = new Vector2(26f, -524f);
            ert.offsetMax = new Vector2(-26f, -404f);

            Transform exBg = GetOrCreate(extra, "Bg");
            SetSliced(Img(exBg), "UI/extra_bg", false);
            Stretch(exBg);
            exBg.SetAsFirstSibling();

            TextMeshProUGUI exLabel = NewText(extra, "Label", "■ 数据 / DATA", FS_LABEL, Amber,
                TextAlignmentOptions.TopLeft);
            Place(exLabel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -12f), new Vector2(400f, 24f));

            Transform exScroll = GetOrCreate(extra, "ExtraScroll");
            ScrollRect exsr = exScroll.GetComponent<ScrollRect>();
            if (exsr == null) exsr = exScroll.gameObject.AddComponent<ScrollRect>();
            StretchTo(exScroll, 18f, 10f, -18f, -42f);

            Transform exView = GetOrCreate(exScroll, "Viewport");
            StretchTo(exView, 0f, 0f, -14f, 0f);
            if (exView.GetComponent<RectMask2D>() == null) exView.gameObject.AddComponent<RectMask2D>();

            Transform exText = exView.Find("ExtraInfoText");
            Transform oldEx = extra.Find("ExtraInfoText");
            if (exText == null && oldEx != null) { oldEx.SetParent(exView, false); exText = oldEx; }
            else if (exText != null && oldEx != null) { Object.DestroyImmediate(oldEx.gameObject); }
            if (exText == null) exText = GetOrCreate(exView, "ExtraInfoText");

            TextMeshProUGUI ex = Text(exText);
            Style(ex, "—", FS_LABEL, BoneDim, TextAlignmentOptions.TopLeft);
            ex.lineSpacing = 8f;
            RectTransform xrt = RT(exText);
            xrt.anchorMin = new Vector2(0f, 1f);
            xrt.anchorMax = new Vector2(1f, 1f);
            xrt.pivot = new Vector2(0.5f, 1f);
            xrt.anchoredPosition = new Vector2(0f, -4f);
            xrt.sizeDelta = new Vector2(0f, 40f);
            ContentSizeFitter exCsf = exText.GetComponent<ContentSizeFitter>();
            if (exCsf == null) exCsf = exText.gameObject.AddComponent<ContentSizeFitter>();
            exCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            exCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Scrollbar exBar = BuildScrollbar(exScroll, "Scrollbar Vertical",
                "UI/scroll_track", "UI/scroll_thumb", 12f);
            exsr.content = xrt;
            exsr.viewport = RT(exView);
            exsr.horizontal = false;
            exsr.vertical = true;
            exsr.movementType = ScrollRect.MovementType.Clamped;
            exsr.scrollSensitivity = 28f;
            exsr.verticalScrollbar = exBar;
            exsr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            ScrollbarAutoHide eah = exScroll.GetComponent<ScrollbarAutoHide>();
            if (eah == null) eah = exScroll.gameObject.AddComponent<ScrollbarAutoHide>();
            eah.bar = exBar;
        }

        // ── 关键词行 ──
        Transform kw = GetOrCreate(right, "KeywordRow");
        RectTransform krt = RT(kw);
        krt.anchorMin = new Vector2(0f, 1f);
        krt.anchorMax = new Vector2(1f, 1f);
        krt.pivot = new Vector2(0.5f, 1f);
        krt.offsetMin = new Vector2(26f, -580f);
        krt.offsetMax = new Vector2(-26f, -536f);

        TextMeshProUGUI kwLabel = NewText(kw, "Label", "关键词", FS_LABEL, BoneDark, TextAlignmentOptions.Left);
        Place(kwLabel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            Vector2.zero, new Vector2(110f, 30f));

        Transform kwList = GetOrCreate(kw, "List");
        Place(kwList, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(110f, 0f), new Vector2(800f, 30f));
        HorizontalLayoutGroup khlg = kwList.GetComponent<HorizontalLayoutGroup>();
        if (khlg == null) khlg = kwList.gameObject.AddComponent<HorizontalLayoutGroup>();
        khlg.spacing = 8f;
        khlg.childAlignment = TextAnchor.MiddleLeft;
        khlg.childControlWidth = true;
        khlg.childControlHeight = true;
        khlg.childForceExpandWidth = false;
        khlg.childForceExpandHeight = false;

        // ── 关联档案 ──
        Transform related = GetOrCreate(right, "RelatedPanel");
        RectTransform rprt = RT(related);
        rprt.anchorMin = new Vector2(0f, 1f);
        rprt.anchorMax = new Vector2(1f, 1f);
        rprt.pivot = new Vector2(0.5f, 1f);
        rprt.offsetMin = new Vector2(26f, -660f);
        rprt.offsetMax = new Vector2(-26f, -594f);

        TextMeshProUGUI relLabel = NewText(related, "Label", "关联档案", FS_LABEL, BoneDark,
            TextAlignmentOptions.TopLeft);
        Place(relLabel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(300f, 28f));
        Transform relList = GetOrCreate(related, "List");
        Place(relList, new Vector2(0f, 0f), new Vector2(0f, 0f),
            Vector2.zero, new Vector2(1000f, 48f));
        HorizontalLayoutGroup rhlg = relList.GetComponent<HorizontalLayoutGroup>();
        if (rhlg == null) rhlg = relList.gameObject.AddComponent<HorizontalLayoutGroup>();
        rhlg.spacing = 10f;
        rhlg.childAlignment = TextAnchor.MiddleLeft;
        rhlg.childControlWidth = false;
        rhlg.childControlHeight = false;
        rhlg.childForceExpandWidth = false;
        rhlg.childForceExpandHeight = false;

        // ── 底部栏 ──
        Transform foot = GetOrCreate(right, "FootBar");
        RectTransform frt = RT(foot);
        frt.anchorMin = new Vector2(0f, 0f);
        frt.anchorMax = new Vector2(1f, 0f);
        frt.pivot = new Vector2(0.5f, 0f);
        frt.offsetMin = new Vector2(26f, 12f);
        frt.offsetMax = new Vector2(-26f, 38f);

        TextMeshProUGUI fL = NewText(foot, "Hint", "按 ESC 返回主菜单", FS_META, BoneDark,
            TextAlignmentOptions.Left);
        Stretch(fL.transform);
        TextMeshProUGUI fR = NewText(foot, "Source", "", FS_META, Hex("#574A3F"),
            TextAlignmentOptions.Right);
        Stretch(fR.transform);
    }

    // ------------------------------------------------------------------ 绑定
    static void BindAll(Transform root)
    {
        CodexUIController ctrl = Object.FindObjectOfType<CodexUIController>();
        if (ctrl == null) return;

        SerializedObject so = new SerializedObject(ctrl);
        BindGraphic(so, "progressText", Find(root, "TopBar/Progress/ProgressText"));
        BindGraphic(so, "progressFill", Find(root, "TopBar/Progress/BarFill"));
        BindGraphic(so, "unlockStatusText", Find(root, "MainArea/RightPanel/Stamp/Text"));
        BindTransform(so, "relatedEntriesParent", Find(root, "MainArea/RightPanel/RelatedPanel/List"));
        BindGraphic(so, "titleText", Find(root, "MainArea/RightPanel/TitleText"));
        BindGraphic(so, "descriptionText", Find(root, "MainArea/RightPanel/DescScroll/Viewport/DescirptionText"));
        BindGraphic(so, "iconImage", Find(root, "MainArea/RightPanel/IconImage/Glyph"));
        BindGraphic(so, "extraInfoText", Find(root, "MainArea/RightPanel/ExtraInfoContainer/ExtraScroll/Viewport/ExtraInfoText"));
        BindGameObject(so, "extraInfoContainer", Find(root, "MainArea/RightPanel/ExtraInfoContainer"));
        BindGraphic(so, "categoryChipText", Find(root, "MainArea/RightPanel/Chips/CategoryChip/Label"));
        BindGraphic(so, "entryIdText", Find(root, "MainArea/RightPanel/Chips/IdChip/Label"));
        BindGraphic(so, "subText", Find(root, "MainArea/RightPanel/SubText"));
        BindGraphic(so, "footerCountText", Find(root, "MainArea/LeftPanel/Footer/Count"));
        BindGraphic(so, "footerSourceText", Find(root, "MainArea/RightPanel/FootBar/Source"));
        BindTransform(so, "keywordParent", Find(root, "MainArea/RightPanel/KeywordRow/List"));

        SerializedProperty idle = so.FindProperty("tabIdleSprite");
        if (idle != null) idle.objectReferenceValue = S("UI/tab_idle");
        SerializedProperty active = so.FindProperty("tabActiveSprite");
        if (active != null) active.objectReferenceValue = S("UI/tab_active");
        SerializedProperty chip = so.FindProperty("keywordChipSprite");
        if (chip != null) chip.objectReferenceValue = S("UI/btn_normal");
        SerializedProperty relPrefab = so.FindProperty("relatedEntryPrefab");
        if (relPrefab != null)
            relPrefab.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/UIPrefabs/Codex/RelatedChip.prefab");

        SerializedProperty dbs = so.FindProperty("categoryDatabases");
        if (dbs != null && dbs.arraySize >= 6)
        {
            CodexDatabase building = AssetDatabase.LoadAssetAtPath<CodexDatabase>(
                "Assets/Scripts/UI/Codex/Building/Building.asset");
            CodexDatabase passive = AssetDatabase.LoadAssetAtPath<CodexDatabase>(
                "Assets/Scripts/UI/Codex/Passive/Passive.asset");
            if (building != null) dbs.GetArrayElementAtIndex(3).objectReferenceValue = building;
            if (passive != null) dbs.GetArrayElementAtIndex(4).objectReferenceValue = passive;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BindGraphic(SerializedObject so, string field, Transform target)
    {
        if (target == null) return;
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        Graphic g = target.GetComponent<Graphic>();
        if (g != null) p.objectReferenceValue = g;
    }

    static void BindTransform(SerializedObject so, string field, Transform target)
    {
        if (target == null) return;
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        p.objectReferenceValue = target;
    }

    static void BindGameObject(SerializedObject so, string field, Transform target)
    {
        if (target == null) return;
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        p.objectReferenceValue = target.gameObject;
    }

    // ------------------------------------------------------------------ 6. Prefab
    [MenuItem("Tools/PixelWasteland/6 - Rebuild Codex Prefabs")]
    public static void RebuildPrefabs()
    {
        BuildCategoryTab();
        BuildEntryButton();
        BuildRelatedChip();
        AssetDatabase.SaveAssets();
        Debug.Log("[PixelWasteland] Prefab 重制完成");
    }

    static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(t.GetChild(i).gameObject);
    }

    static void BuildCategoryTab()
    {
        string path = "Assets/Prefabs/UI/UIPrefabs/Codex/CategoryTab.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        ClearChildren(root.transform);

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(290f, TABS_H);

        LayoutElement le = root.GetComponent<LayoutElement>();
        if (le == null) le = root.AddComponent<LayoutElement>();
        le.minWidth = 150f;
        le.flexibleWidth = 1f;

        Image img = root.GetComponent<Image>();
        if (img == null) img = root.AddComponent<Image>();
        SetSliced(img, "UI/tab_idle", true);

        Button btn = root.GetComponent<Button>();
        if (btn == null) btn = root.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.88f, 0.80f, 0.62f, 1f);
        cb.pressedColor = new Color(0.70f, 0.62f, 0.48f, 1f);
        cb.selectedColor = Color.white;
        btn.colors = cb;

        // 名称 + 英文角标作为一个整体居中，避免角标飘在右边脱离视觉
        Transform row = GetOrCreate(root.transform, "Row");
        RectTransform rowRt = RT(row);
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.anchoredPosition = Vector2.zero;
        rowRt.sizeDelta = new Vector2(200f, 30f);
        HorizontalLayoutGroup rh = row.GetComponent<HorizontalLayoutGroup>();
        if (rh == null) rh = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rh.spacing = 10f;
        rh.childAlignment = TextAnchor.MiddleCenter;
        rh.childControlWidth = true;
        rh.childControlHeight = true;
        rh.childForceExpandWidth = false;
        rh.childForceExpandHeight = false;
        ContentSizeFitter rcsf = row.GetComponent<ContentSizeFitter>();
        if (rcsf == null) rcsf = row.gameObject.AddComponent<ContentSizeFitter>();
        rcsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rcsf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        NewText(row, "Label", "分类", FS_BODY, BoneDim, TextAlignmentOptions.Center);
        NewText(row, "Code", "CV", FS_META, BoneDark, TextAlignmentOptions.Center);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void BuildEntryButton()
    {
        string path = "Assets/Prefabs/UI/UIPrefabs/Codex/EntryButton.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        ClearChildren(root.transform);

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, ROW_H);

        Image img = root.GetComponent<Image>();
        if (img == null) img = root.AddComponent<Image>();
        SetSliced(img, "UI/btn_normal", true);

        Button btn = root.GetComponent<Button>();
        if (btn == null) btn = root.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.82f, 0.68f, 0.50f, 1f);
        cb.pressedColor = new Color(0.62f, 0.52f, 0.40f, 1f);
        cb.selectedColor = Color.white;
        btn.colors = cb;

        LayoutElement le = root.GetComponent<LayoutElement>();
        if (le == null) le = root.AddComponent<LayoutElement>();
        le.preferredHeight = ROW_H;
        le.minHeight = ROW_H;

        Image icon = NewImage(root.transform, "Icon", "Icons/card", new Vector2(ROW_ICON, ROW_ICON));
        Place(icon.transform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(30f, 0f), new Vector2(ROW_ICON, ROW_ICON));

        TextMeshProUGUI name = NewText(root.transform, "Name", "条目名称", FS_BODY, Bone,
            TextAlignmentOptions.Left);
        RectTransform nrt = RT(name.transform);
        nrt.anchorMin = new Vector2(0f, 0f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.offsetMin = new Vector2(58f, 0f);
        nrt.offsetMax = new Vector2(-230f, 0f);

        TextMeshProUGUI id = NewText(root.transform, "Id", "#ID", FS_META, BoneDark,
            TextAlignmentOptions.Right);
        RectTransform irt = RT(id.transform);
        irt.anchorMin = new Vector2(1f, 0.5f);
        irt.anchorMax = new Vector2(1f, 0.5f);
        irt.pivot = new Vector2(1f, 0.5f);
        irt.anchoredPosition = new Vector2(-58f, 0f);
        irt.sizeDelta = new Vector2(170f, 18f);

        Image unlock = NewImage(root.transform, "UnlockIcon", "Icons/lock", new Vector2(32f, 32f));
        Place(unlock.transform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-30f, 0f), new Vector2(32f, 32f));

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void BuildRelatedChip()
    {
        string dir = "Assets/Prefabs/UI/UIPrefabs/Codex";
        string path = dir + "/RelatedChip.prefab";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Prefabs/UI/UIPrefabs", "Codex");

        GameObject root;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            root = PrefabUtility.LoadPrefabContents(path);
        else
            root = new GameObject("RelatedChip", typeof(RectTransform), typeof(CanvasRenderer));

        ClearChildren(root.transform);

        RectTransform rt = root.GetComponent<RectTransform>();
        if (rt == null) rt = root.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(220f, 44f);

        Image img = root.GetComponent<Image>();
        if (img == null) img = root.AddComponent<Image>();
        SetSliced(img, "UI/btn_normal", true);

        Button btn = root.GetComponent<Button>();
        if (btn == null) btn = root.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.86f, 0.74f, 0.56f, 1f);
        cb.pressedColor = new Color(0.66f, 0.56f, 0.42f, 1f);
        btn.colors = cb;

        LayoutElement le = root.GetComponent<LayoutElement>();
        if (le == null) le = root.AddComponent<LayoutElement>();
        le.preferredWidth = 220f;
        le.preferredHeight = 44f;

        TextMeshProUGUI label = NewText(root.transform, "Label", "关联", FS_LABEL, BoneDim,
            TextAlignmentOptions.Center);
        RectTransform lrt = RT(label.transform);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(12f, 0f);
        lrt.offsetMax = new Vector2(-12f, 0f);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
