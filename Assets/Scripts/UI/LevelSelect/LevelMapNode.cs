using System;
using DoudizhuTower.Config;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoudizhuTower.UI.LevelSelect
{
    /// <summary>
    /// 地图上的单个关卡节点。
    /// 只负责「显示自己的状态」和「把点击转给控制器」，不做布局、不做跳转。
    /// 未解锁节点在 OnPointerClick 里硬拦截，不依赖视觉变灰。
    /// </summary>
    public class LevelMapNode : MonoBehaviour, IPointerClickHandler
    {
        [Header("视觉引用")]
        [SerializeField] private Image frame;
        [SerializeField] private Image thumbnail;
        [SerializeField] private Image selectionRing;
        [SerializeField] private GameObject lockBadge;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private CanvasGroup group;

        [Header("配色（临时占位，正式美术可覆盖）")]
        [SerializeField] private Color normalTint = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color lockedTint = new Color(0.45f, 0.42f, 0.38f, 1f);

        private Action<LevelMapNode> _onClick;
        private Image _lockImage;

        /// <summary>在「排序后的关卡列表」中的下标</summary>
        public int Index { get; private set; }

        public LevelConfig Config { get; private set; }

        /// <summary>已把调试锁计算在内的最终可交互状态</summary>
        public bool IsUnlocked { get; private set; }

        public void Setup(LevelConfig config, int index, bool unlocked, Action<LevelMapNode> onClick)
        {
            Config = config;
            Index = index;
            IsUnlocked = unlocked;
            _onClick = onClick;

            if (nameText != null)
                nameText.text = config != null ? config.levelName : "—";

            if (difficultyText != null)
            {
                int d = config != null ? Mathf.Clamp(config.difficulty, 0, 5) : 0;
                string s = "";
                for (int i = 0; i < d; i++) s += "★";
                difficultyText.text = s;
                difficultyText.color = unlocked
                    ? new Color(0.94f, 0.63f, 0.31f)
                    : new Color(0.47f, 0.41f, 0.33f);
            }

            if (thumbnail != null)
            {
                thumbnail.sprite = config != null ? config.thumbnail : null;
                thumbnail.enabled = thumbnail.sprite != null;
                thumbnail.color = unlocked ? Color.white : new Color(0.55f, 0.5f, 0.45f, 1f);
            }

            // 锁标：GameObject 与 Image.enabled 必须同时受控，否则会出现"激活了但透明"
            if (lockBadge != null)
            {
                if (_lockImage == null) _lockImage = lockBadge.GetComponent<Image>();
                lockBadge.SetActive(!unlocked);
                if (_lockImage != null) _lockImage.enabled = !unlocked;
            }

            if (frame != null) frame.color = unlocked ? normalTint : lockedTint;
            if (group != null) group.alpha = unlocked ? 1f : 0.75f;

            SetSelected(false);
        }

        public void SetSelected(bool on)
        {
            if (selectionRing != null) selectionRing.enabled = on;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 硬拦截：未解锁节点不产生任何回调
            if (!IsUnlocked) return;
            if (_onClick != null) _onClick(this);
        }
    }
}
