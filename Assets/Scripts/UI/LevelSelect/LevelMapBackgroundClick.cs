using UnityEngine;
using UnityEngine.EventSystems;

namespace DoudizhuTower.UI.LevelSelect
{
    /// <summary>
    /// 地图空白区域点击检测。
    /// 挂在 ScrollView 上：只有「点击命中的是地图背景」时才会收到事件，
    /// 因为节点自身实现了 IPointerClickHandler，ExecuteHierarchy 会在节点处终止，不会冒泡到这里。
    /// 拖动时 uGUI 会把 eligibleForClick 置 false，因此拖动结束不会误触发。
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelMapBackgroundClick : MonoBehaviour, IPointerClickHandler, IBeginDragHandler
    {
        [SerializeField] private LevelMapController controller;

        public void Setup(LevelMapController c) { controller = c; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // 玩家开始拖动地图时，立刻取消自动避让动画，避免与拖动互相打架
            if (controller != null) controller.CancelScrollAnimation();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.dragging) return;   // 双保险：拖动中不响应
            if (controller != null) controller.CloseDetail();
        }
    }
}
