using UnityEngine;
using TMPro;
using DoudizhuTower.Gameplay.Fusion;

namespace DoudizhuTower.Gameplay.Fusion.UI
{
    /// <summary>
    /// 金币只读视图。
    /// 只读取 PlayerState.Gold，不持有任何游戏对象引用。
    /// </summary>
    public class GoldView : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI incomeText;

        /// <summary>
        /// 根据 PlayerState 渲染金币。
        /// </summary>
        public void Render(PlayerState player)
        {
            if (goldText != null)
                goldText.text = Mathf.FloorToInt(player.Gold).ToString();

            if (incomeText != null)
                incomeText.text = $"+{player.IncomeRate:F0}/秒";
        }
    }
}
