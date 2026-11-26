using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Theme
{
    /// <summary>
    /// 通用状态着色（悬浮 / 按下 / 选中）。
    /// 采用「叠加层」上色：不改按钮自身的 sprite/color，因此能和控制器换图共存。
    /// 对应预览里的 .btn:hover / .btn:active / .row.sel。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIStateTint : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        public enum Kind { Button, Tab, Row, Chip }

        public Kind kind = Kind.Button;
        public CodexPalette palette;
        public bool isSelectable;
        public bool wired;

        private Image _bg;
        private Sprite _origSprite;
        private Image _overlay;
        private Image _leftBar;
        private Image _outline;
        private TextMeshProUGUI[] _labels;
        private RectTransform _rt;
        private Vector2 _home;

        private bool _hover, _press, _selected, _ready;

        // ───────────────────────────────────────── 构建
        public void Build(CodexPalette p, Kind k, bool selectable)
        {
            palette = p;
            kind = k;
            isSelectable = selectable;

            _rt = transform as RectTransform;
            if (_rt != null) _home = _rt.anchoredPosition;
            _bg = GetComponent<Image>();
            _labels = GetComponentsInChildren<TextMeshProUGUI>(true);

            _overlay = MakeLayer("__TintOverlay", new Vector2(0f, 0f), 0f);
            if (_overlay != null) _overlay.color = new Color(1f, 1f, 1f, 0f);

            if (kind == Kind.Row || kind == Kind.Tab)
            {
                _leftBar = MakeLayer("__TintLeftBar", new Vector2(0f, 0f), 0f);
                if (_leftBar != null)
                {
                    var rt = (RectTransform)_leftBar.transform;
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.offsetMin = new Vector2(0f, 0f);
                    rt.offsetMax = new Vector2(4f, 0f);
                    _leftBar.color = new Color(1f, 1f, 1f, 0f);
                }
            }

            _ready = true;
            Apply();
        }

        private Image MakeLayer(string name, Vector2 padding, float inset)
        {
            var existing = transform.Find(name);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
            }
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = padding * -1f;
            rt.offsetMax = padding;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.enabled = true;
            int idx = name.Contains("LeftBar") ? 1 : 0;
            go.transform.SetSiblingIndex(Mathf.Min(idx, transform.childCount - 1));
            return img;
        }

        public void SetSelected(bool on) { _selected = on; Apply(); }

        // ───────────────────────────────────────── 指针
        public void OnPointerEnter(PointerEventData e) { _hover = true; Apply(); }
        public void OnPointerExit(PointerEventData e) { _hover = false; _press = false; Apply(); }
        public void OnPointerDown(PointerEventData e) { _press = true; Apply(); }
        public void OnPointerUp(PointerEventData e) { _press = false; Apply(); }

        // ───────────────────────────────────────── 上色
        private void Apply()
        {
            if (!_ready || palette == null) return;

            var btn = GetComponent<Button>();
            bool ok = btn == null || btn.interactable;

            Color overlay = new Color(1f, 1f, 1f, 0f);
            Color bar = new Color(1f, 1f, 1f, 0f);
            Color text = palette.normalText;
            bool offset = false;

            if (!ok)
            {
                overlay = Fade(palette.steel0, 0.35f);
                text = palette.boneDark;
            }
            else if (_press)
            {
                overlay = Fade(palette.steel0, 0.45f);
                text = palette.pressText;
                offset = true;
            }
            else if (_hover)
            {
                bool isRow = kind == Kind.Row;
                overlay = isRow ? Fade(palette.amber, 0.10f) : Fade(palette.steel3, 0.55f);
                text = palette.hoverText;
                if (isRow) bar = palette.amberDim;
            }
            else if (_selected && isSelectable)
            {
                overlay = Fade(palette.toxic, 0.18f);
                bar = palette.toxic;
                text = palette.selectedText;
            }
            else
            {
                text = kind == Kind.Row || kind == Kind.Tab ? palette.boneDim : palette.normalText;
            }

            if (_overlay != null) _overlay.color = overlay;
            if (_leftBar != null) _leftBar.color = bar;

            if (_labels != null)
                foreach (var l in _labels)
                    if (l != null) l.color = text;

            // 按下反馈：只改缩放，绝不碰 anchoredPosition（否则布局组会打架）
            if (_rt != null)
                _rt.localScale = offset ? Vector3.one * palette.pressScale : Vector3.one;

            // 按下换贴图（斜面明暗反转），仅普通按钮，避免和控制器换图冲突
            if (kind == Kind.Button && _bg != null)
            {
                if (offset)
                {
                    if (_origSprite == null) _origSprite = _bg.sprite;
                    if (palette.pressedSprite != null) _bg.sprite = palette.pressedSprite;
                }
                else if (_origSprite != null)
                {
                    _bg.sprite = _origSprite;
                    _origSprite = null;
                }
            }
        }

        private static Color Fade(Color c, float a) { c.a = a; return c; }
    }
}
