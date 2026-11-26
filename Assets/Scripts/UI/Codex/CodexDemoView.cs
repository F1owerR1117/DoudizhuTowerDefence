using System.Collections.Generic;
using DoudizhuTower.Config;
using DoudizhuTower.Gameplay.Systems;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Codex · UI Toolkit 视图（目标：1:1 复刻 PixelWasteland_Codex_Preview.html）
/// 数据仍来自 CodexDatabase：新增条目只需添加 .asset。
/// </summary>
public class CodexDemoView : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private CodexDatabase[] databases;
    [SerializeField] private Font pixelFont;

    private readonly string[] _names = { "卡牌数值", "牌型", "Boss", "建筑", "被动", "规则" };
    private readonly string[] _codes = { "CV", "CT", "BS", "BD", "PS", "RL" };

    private const int SEGMENTS = 28;

    private VisualElement _root;
    private VisualElement _tabs, _keywordList, _related, _barBg, _catIcon, _socketIcon;
    private ListView _list;
    private TextField _search;
    private Label _title, _sub, _desc, _extra, _count, _footer, _catChip, _idChip, _stamp, _progLabel, _badge, _statCount, _metaId, _metaCat, _metaState;

    private int _cat;
    private string _query = string.Empty;
    private readonly List<CodexEntry> _items = new List<CodexEntry>();
    private readonly List<VisualElement> _segs = new List<VisualElement>();

    private void OnEnable()
    {
        VisualElement root = document != null ? document.rootVisualElement : null;
        if (root == null) return;

        _tabs = root.Q<VisualElement>("tabs");
        _list = root.Q<ListView>("list");
        _search = root.Q<TextField>("search");
        _title = root.Q<Label>("title");
        _sub = root.Q<Label>("sub");
        _desc = root.Q<Label>("desc");
        _extra = root.Q<Label>("extra");
        _count = root.Q<Label>("count");
        _footer = root.Q<Label>("footer");
        _catChip = root.Q<Label>("catChip");
        _idChip = root.Q<Label>("idChip");
        _stamp = root.Q<Label>("stamp");
        _progLabel = root.Q<Label>("progLabel");
        _badge = root.Q<Label>("badge");
        _statCount = root.Q<Label>("statCount");
        _metaId = root.Q<Label>("metaId");
        _metaCat = root.Q<Label>("metaCat");
        _metaState = root.Q<Label>("metaState");
        _keywordList = root.Q<VisualElement>("keywordList");
        _related = root.Q<VisualElement>("related");
        _catIcon = root.Q<VisualElement>("catIcon");
        _socketIcon = root.Q<VisualElement>("socketIcon");
        _barBg = root.Q<VisualElement>("barBg");

        _root = root;

        IgnorOverlays(root);
        BuildProgress();
        BuildTabs();

        if (_search != null)
            _search.RegisterValueChangedCallback(evt =>
            {
                _query = evt.newValue ?? string.Empty;
                RefreshList();
            });

        if (_list != null)
        {
            _list.makeItem = MakeRow;
            _list.bindItem = BindRow;
            _list.fixedItemHeight = 74;
            _list.selectionChanged += OnSelect;
        }

        Button back = root.Q<Button>("back");
        if (back != null) back.clicked += () => { PlayClick(); SceneLoader.LoadMainMenu(); };

        RefreshList();
        if (_root != null) _root.schedule.Execute(StyleScroller).ExecuteLater(300);
    }

    // ── 进度条 28 段 ──
    private void BuildProgress()
    {
        if (_barBg == null) return;
        _barBg.Clear();
        _segs.Clear();
        for (int i = 0; i < SEGMENTS; i++)
        {
            var seg = new VisualElement();
            seg.AddToClassList("seg");
            _barBg.Add(seg);
            _segs.Add(seg);
        }
    }

    // ── 页签 ──
    private void BuildTabs()
    {
        if (_tabs == null) return;
        _tabs.Clear();
        for (int i = 0; i < _names.Length; i++)
        {
            int idx = i;
            var btn = new Button();
            btn.AddToClassList("tab");
            var lbl = new Label(_names[i]);
            lbl.AddToClassList("tab-label");
            var code = new Label(_codes[i]);
            code.AddToClassList("tab-code");
            btn.Add(lbl);
            btn.Add(code);
            btn.clicked += () =>
            {
                _cat = idx;
                PlayClick();
                RefreshTabs();
                RefreshList();
            };
            _tabs.Add(btn);
        }
        RefreshTabs();
    }

    private void RefreshTabs()
    {
        if (_tabs == null) return;
        for (int i = 0; i < _tabs.childCount; i++)
            _tabs[i].EnableInClassList("active", i == _cat);
    }

    // ── 列表 ──
    private VisualElement MakeRow()
    {
        var row = new VisualElement();
        row.AddToClassList("row");
        var icon = new Image { name = "ic" };
        icon.AddToClassList("row-icon");
        row.Add(icon);
        var name = new Label { name = "n" };
        name.AddToClassList("row-name");
        var id = new Label { name = "id" };
        id.AddToClassList("row-id");
        row.Add(name);
        row.Add(id);
        return row;
    }

    private void BindRow(VisualElement el, int index)
    {
        if (index < 0 || index >= _items.Count) return;
        CodexEntry e = _items[index];
        el.Q<Label>("n").text = e.DisplayName;
        var ic = el.Q<Image>("ic");
        if (ic != null && e.Icon != null) ic.sprite = e.Icon;
        el.Q<Label>("id").text = string.IsNullOrEmpty(e.Id) ? "" : "#" + e.Id.ToUpperInvariant();
    }

    private void RefreshList()
    {
        _items.Clear();
        CodexDatabase db = databases != null && _cat < databases.Length ? databases[_cat] : null;
        if (db != null)
        {
            if (db.Entries == null) db.Entries = new List<CodexEntry>();
            string q = (_query ?? string.Empty).Trim().ToLowerInvariant();
            foreach (CodexEntry e in db.Entries)
            {
                if (e == null) continue;
                if (q.Length > 0)
                {
                    bool hit = e.DisplayName != null && e.DisplayName.ToLowerInvariant().Contains(q);
                    if (!hit && e.Keywords != null)
                        for (int i = 0; i < e.Keywords.Length; i++)
                            if (e.Keywords[i] != null && e.Keywords[i].ToLowerInvariant().Contains(q)) { hit = true; break; }
                    if (!hit) continue;
                }
                _items.Add(e);
            }
        }

        if (_list != null) { _list.itemsSource = _items; _list.Rebuild(); }

        if (_count != null)
            _count.text = string.Format("共 {0} 条 / 全部 {1} 条", _items.Count,
                db != null && db.Entries != null ? db.Entries.Count : 0);
        if (_badge != null)
            _badge.text = (_items.Count < 10 ? "0" : "") + _items.Count;
        if (_catChip != null) _catChip.text = _names[_cat];
        if (_statCount != null) _statCount.text = "缓存 " + _items.Count + " 条";

        UpdateProgress(db);
        ShowDetail(_items.Count > 0 ? _items[0] : null);
        ApplyFont();
    }

    private void UpdateProgress(CodexDatabase db)
    {
        int total = db != null && db.Entries != null ? db.Entries.Count : 0;
        int open = 0;
        if (db != null && db.Entries != null)
            foreach (CodexEntry e in db.Entries)
                if (e != null && SaveSystem.IsCodexEntryUnlocked(e.Id)) open++;

        if (_progLabel != null)
            _progLabel.text = string.Format("已解档 {0} / {1}", open, total);

        int lit = total > 0 ? Mathf.RoundToInt(open * (float)_segs.Count / total) : 0;
        for (int i = 0; i < _segs.Count; i++)
            _segs[i].EnableInClassList("on", i < lit);
    }

    private void OnSelect(IEnumerable<object> selection)
    {
        foreach (object o in selection)
        {
            var e = o as CodexEntry;
            if (e != null) { PlayClick(); ShowDetail(e); }
            break;
        }
    }

    // ── 详情 ──
    private void ShowDetail(CodexEntry e)
    {
        if (e == null)
        {
            if (_title != null) _title.text = "选择档案";
            if (_sub != null) _sub.text = "";
            if (_desc != null) _desc.text = "从左侧列表选择一条记录，查看它的详细说明与关联档案。";
            if (_extra != null) _extra.text = "—";
            if (_idChip != null) _idChip.text = "—";
            if (_stamp != null) _stamp.style.display = DisplayStyle.None;
            if (_keywordList != null) _keywordList.Clear();
            if (_related != null) _related.Clear();
            if (_footer != null) _footer.text = "";
            if (_socketIcon != null) _socketIcon.style.display = DisplayStyle.None;
            return;
        }

        string id = string.IsNullOrEmpty(e.Id) ? "" : e.Id.ToUpperInvariant();
        if (_title != null) _title.text = e.DisplayName;
        if (_sub != null) _sub.text = id.Replace('-', ' ');
        if (_idChip != null) _idChip.text = id.Length > 0 ? id : "—";
        if (_desc != null) _desc.text = e.Description;
        if (_extra != null) _extra.text = string.IsNullOrEmpty(e.ExtraInfo) ? "—" : e.ExtraInfo;
        if (_footer != null) _footer.text = "SOURCE · CodexDatabase / " + e.Category;

        bool unlocked = SaveSystem.IsCodexEntryUnlocked(e.Id);
        if (_metaId != null) _metaId.text = id.Length > 0 ? id : "—";
        if (_metaCat != null) _metaCat.text = _names[_cat];
        if (_metaState != null) _metaState.text = unlocked ? "已归档" : "未解档";
        if (_stamp != null)
        {
            _stamp.style.display = DisplayStyle.Flex;
            _stamp.text = unlocked ? "已解档" : "未解档";
            _stamp.EnableInClassList("on", unlocked);
            _stamp.EnableInClassList("off", !unlocked);
        }

        Sprite icon = e.Icon;
        if (_socketIcon != null)
        {
            _socketIcon.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (icon != null) _socketIcon.style.backgroundImage = new StyleBackground(icon);
        }
        if (_catIcon != null && icon != null)
            _catIcon.style.backgroundImage = new StyleBackground(icon);

        if (_keywordList != null)
        {
            _keywordList.Clear();
            if (e.Keywords != null)
                foreach (string k in e.Keywords)
                {
                    var kw = new Label(k);
                    kw.AddToClassList("kw");
                    _keywordList.Add(kw);
                }
        }

        if (_related != null)
        {
            _related.Clear();
            if (e.RelatedEntries != null && databases != null && _cat < databases.Length)
            {
                CodexDatabase db = databases[_cat];
                foreach (string rid in e.RelatedEntries)
                {
                    CodexEntry target = db != null ? db.GetById(rid) : null;
                    if (target == null) continue;
                    if (_related.childCount == 0)
                    {
                        var relLabel = new Label("关联档案");
                        relLabel.AddToClassList("kw-label");
                        _related.Add(relLabel);
                    }
                    var btn = new Button { text = target.DisplayName };
                    btn.AddToClassList("rel-btn");
                    CodexEntry captured = target;
                    btn.clicked += () => { PlayClick(); ShowDetail(captured); };
                    _related.Add(btn);
                }
            }
        }
    }

    /// <summary>主题给每个文本元素单独设了字体，需逐个覆盖才能用上像素字体</summary>
    private void ApplyFont()
    {
        if (pixelFont == null || _root == null) return;
        StyleScroller();
        _root.Query<TextElement>().ForEach(t =>
        {
            t.style.unityFont = pixelFont;
            t.style.unityFontDefinition = new StyleFontDefinition(pixelFont);
        });
    }

    /// <summary>全屏覆盖层与投影层必须不接收点击，否则整屏无法交互</summary>
    private void IgnorOverlays(VisualElement root)
    {
        string[] cls = { "overlay", "ov-scan", "ov-vignette", "ov-glass", "panel-shadow" };
        foreach (string c in cls)
        {
            foreach (var e in UnityEngine.UIElements.UQueryExtensions.Query<VisualElement>(root, className: c).ToList())
                e.pickingMode = PickingMode.Ignore;
        }
    }

    private bool _scrollerStyled;

    /// <summary>USS 类名在不同版本不一致，直接抓到 Scroller/Dragger 上色</summary>
    private void StyleScroller()
    {
        if (_scrollerStyled || _list == null) return;
        var sc = _list.Query<Scroller>().First();
        if (sc == null) return;

        sc.style.display = DisplayStyle.None;   // 预览里无可见滚动条，隐藏后仍支持滚轮
        sc.style.backgroundColor = new Color(0.039f, 0.075f, 0.063f, 1f);

        var dc = sc.Q<VisualElement>(null, "unity-drag-container");
        if (dc != null) dc.style.backgroundColor = new Color(0.039f, 0.075f, 0.063f, 1f);

        var dg = sc.Q<VisualElement>(null, "unity-dragger");
        if (dg != null)
        {
            dg.style.backgroundColor = new Color(0.169f, 0.137f, 0.125f, 1f);
            dg.style.borderTopWidth = 0;
            dg.style.borderBottomWidth = 0;
            dg.style.borderLeftWidth = 0;
            dg.style.borderRightWidth = 0;
        }

        var up = sc.Q<VisualElement>(null, "unity-low-button");
        if (up != null) up.style.display = DisplayStyle.None;
        var dn = sc.Q<VisualElement>(null, "unity-high-button");
        if (dn != null) dn.style.display = DisplayStyle.None;

        _scrollerStyled = true;
    }

    private void PlayClick()
    {
        var audio = AudioManager.Instance;
        if (audio != null) audio.PlayButtonClick();
    }
}
