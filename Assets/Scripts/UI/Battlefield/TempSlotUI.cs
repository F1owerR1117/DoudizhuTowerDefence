using System;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.UI.Hand;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Battlefield
{
    /// <summary>
    /// 暂存槽 UI（§4.4 传送飞筒接收端）。
    /// 最多容纳 1 张牌，支持点击加入手牌或弃置。
    /// </summary>
    public class TempSlotUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private Button addToHandButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private TextMeshProUGUI hintLabel;
        [SerializeField] private GameObject emptyPlaceholder;
        [SerializeField] private CardWidget cardWidget;

        private Card? _heldCard;
        private CardDeck _deck;
        private HandArea _handArea;
        private CardHand _playerHand;
        private CardWidget _cardWidgetInstance;

        /// <summary>槽内是否有牌</summary>
        public bool IsEmpty => !_heldCard.HasValue;

        /// <summary>当前暂存的牌（无牌时为 null）</summary>
        public Card? HeldCard => _heldCard;

        /// <summary>暂存槽清空事件</summary>
        public event Action OnSlotEmptied;

        /// <summary>牌被移入手牌事件</summary>
        public event Action<Card> OnCardMovedToHand;

        /// <summary>牌被弃置事件</summary>
        public event Action<Card> OnCardDiscarded;

        public void Initialize(CardDeck deck, HandArea handArea, CardHand playerHand)
        {
            _deck = deck;
            _handArea = handArea;
            _playerHand = playerHand;

            // 实例化 CardWidget 预制体
            if (cardWidget != null && _cardWidgetInstance == null)
            {
                _cardWidgetInstance = Instantiate(cardWidget, transform);
                _cardWidgetInstance.gameObject.SetActive(false);
            }

            if (addToHandButton != null)
                addToHandButton.onClick.AddListener(OnAddToHandClicked);

            if (discardButton != null)
                discardButton.onClick.AddListener(OnDiscardClicked);

            // 无 HandArea 时为只读模式（队友暂存槽），隐藏交互按钮
            if (handArea == null)
            {
                if (addToHandButton != null) addToHandButton.gameObject.SetActive(false);
                if (discardButton != null) discardButton.gameObject.SetActive(false);
                if (hintLabel != null) hintLabel.gameObject.SetActive(false);
            }

            UpdateDisplay();
        }

        /// <summary>
        /// 接收一张传送来的牌。
        /// 若槽内已有牌，旧牌自动弃置。
        /// </summary>
        public void ReceiveCard(Card card)
        {
            if (_heldCard.HasValue)
                DiscardCurrent();

            _heldCard = card;
            UpdateDisplay();
        }

        /// <summary>
        /// 清空暂存槽（不触发弃置，直接丢弃）。
        /// 用于基地被摧毁 / 对局结束。
        /// </summary>
        public void Clear()
        {
            if (!_heldCard.HasValue) return;

            if (_deck != null && _heldCard.HasValue)
                _deck.Discard(_heldCard.Value);

            _heldCard = null;
            UpdateDisplay();
            OnSlotEmptied?.Invoke();
        }

        /// <summary>
        /// 弃置当前暂存的牌（进入弃牌堆）。
        /// </summary>
        private void DiscardCurrent()
        {
            if (!_heldCard.HasValue) return;

            Card card = _heldCard.Value;
            _deck?.Discard(card);
            _heldCard = null;
            UpdateDisplay();

            OnCardDiscarded?.Invoke(card);
            OnSlotEmptied?.Invoke();
        }

        private void OnAddToHandClicked()
        {
            if (!_heldCard.HasValue) return;
            if (_playerHand == null || _playerHand.IsFull)
            {
                if (hintLabel != null)
                {
                    hintLabel.text = "手牌已满";
                    hintLabel.color = Color.red;
                }
                return;
            }

            Card card = _heldCard.Value;
            _playerHand.Add(card);
            _handArea?.NotifyHandChanged();

            _heldCard = null;
            UpdateDisplay();

            OnCardMovedToHand?.Invoke(card);
            OnSlotEmptied?.Invoke();
        }

        private void OnDiscardClicked()
        {
            DiscardCurrent();
        }

        private void UpdateDisplay()
        {
            bool hasCard = _heldCard.HasValue;

            if (emptyPlaceholder != null)
                emptyPlaceholder.SetActive(!hasCard);

            // 使用 CardWidget 实例显示卡牌
            if (_cardWidgetInstance != null)
            {
                if (hasCard)
                {
                    _cardWidgetInstance.Bind(_heldCard.Value);
                    _cardWidgetInstance.gameObject.SetActive(true);
                }
                else
                {
                    _cardWidgetInstance.gameObject.SetActive(false);
                }
            }

            if (addToHandButton != null)
            {
                bool canAdd = hasCard && _playerHand != null && !_playerHand.IsFull;
                addToHandButton.interactable = canAdd;
            }

            if (discardButton != null)
                discardButton.interactable = hasCard;

            if (hintLabel != null)
            {
                if (!hasCard)
                {
                    hintLabel.text = "";
                }
                else if (_playerHand != null && _playerHand.IsFull)
                {
                    hintLabel.text = "手牌已满";
                    hintLabel.color = Color.red;
                }
                else
                {
                    hintLabel.text = "点击加入手牌";
                    hintLabel.color = Color.white;
                }
            }
        }
    }
}
