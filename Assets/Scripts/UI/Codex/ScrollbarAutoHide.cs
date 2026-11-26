using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 内容不需要滚动时自动隐藏滚动条，避免出现"永远满格却拖不动"的滚动条。
/// 挂在带 ScrollRect 的节点上，bar 指向它自己的滚动条。
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class ScrollbarAutoHide : MonoBehaviour
{
    public Scrollbar bar;

    private ScrollRect _scroll;

    private void Awake()
    {
        _scroll = GetComponent<ScrollRect>();
    }

    private void LateUpdate()
    {
        if (_scroll == null || bar == null || _scroll.content == null || _scroll.viewport == null) return;

        bool need = _scroll.content.rect.height > _scroll.viewport.rect.height + 1f;
        if (bar.gameObject.activeSelf != need)
        {
            bar.gameObject.SetActive(need);
            // 隐藏/显示滚动条要重算视口，否则内容宽度不会跟着变
            _scroll.verticalScrollbar = need ? bar : null;
            _scroll.verticalScrollbar = bar;
        }
    }
}
