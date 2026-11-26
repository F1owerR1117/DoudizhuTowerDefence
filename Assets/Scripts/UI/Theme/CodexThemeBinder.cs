using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Theme
{
    /// <summary>
    /// 挂在 Canvas 上：增量给所有 Button 挂 UIStateTint。
    /// 页签与列表行是运行时生成的，所以这里用增量扫描 + 定时补挂，
    /// 不需要改任何布局或预制体。
    /// </summary>
    [DisallowMultipleComponent]
    public class CodexThemeBinder : MonoBehaviour
    {
        public CodexPalette palette;
        public bool applyOnEnable = true;
        public bool trackRowSelection = true;
        [Tooltip("扫描新按钮的间隔（秒）")]
        public float scanInterval = 0.25f;

        private readonly HashSet<Button> _done = new HashSet<Button>();
        private readonly List<UIStateTint> _rows = new List<UIStateTint>();
        private UIStateTint _selectedRow;
        private float _next;

        private void OnEnable()
        {
            if (applyOnEnable) Apply();
        }

        private void Update()
        {
            if (palette == null) return;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + scanInterval;
            Apply();
        }

        [ContextMenu("Apply Theme Now")]
        public void Apply()
        {
            if (palette == null) return;

            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                if (btn == null || _done.Contains(btn)) continue;
                if (btn.GetComponent<Scrollbar>() != null) continue;

                btn.transition = Selectable.Transition.None;

                var tint = btn.GetComponent<UIStateTint>();
                if (tint == null) tint = btn.gameObject.AddComponent<UIStateTint>();

                var kind = Classify(btn);
                tint.Build(palette, kind, kind == UIStateTint.Kind.Row);

                if (kind == UIStateTint.Kind.Row)
                {
                    _rows.Add(tint);
                    if (trackRowSelection && !tint.wired)
                    {
                        tint.wired = true;
                        var captured = tint;
                        btn.onClick.AddListener(() => SelectRow(captured));
                    }
                }

                _done.Add(btn);
            }
        }

        private void SelectRow(UIStateTint row)
        {
            if (_selectedRow == row) return;
            if (_selectedRow != null) _selectedRow.SetSelected(false);
            _selectedRow = row;
            if (row != null) row.SetSelected(true);
        }

        private static UIStateTint.Kind Classify(Button btn)
        {
            Transform p = btn.transform.parent;
            while (p != null)
            {
                string n = p.name;
                if (n == "CategoryTabs") return UIStateTint.Kind.Tab;
                if (n == "Content") return UIStateTint.Kind.Row;
                if (n == "RelatedPanel") return UIStateTint.Kind.Chip;
                if (n == "RightPanel" || n == "LeftPanel" || n == "TopBar") break;
                p = p.parent;
            }
            return UIStateTint.Kind.Button;
        }
    }
}
