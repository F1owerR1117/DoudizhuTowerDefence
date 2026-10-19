using System.Collections.Generic;
using DoudizhuTower.Core.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.HUD
{
    public class CardCounterUI : MonoBehaviour
    {
        [System.Serializable]
        public struct RankCell
        {
            public CardRank rank;
            public TextMeshProUGUI label;       // 点数文字
            public TextMeshProUGUI countText;    // "0/4"
            public Image background;              // 背景图
            public GameObject brokenIndicator;   // 断牌标记（可选）
        }

        [Header("手动拖入格子（按 3~Joker 顺序）")]
        [SerializeField] private RankCell[] cells;

        [Header("牌堆信息")]
        [SerializeField] private TextMeshProUGUI deckRemainingLabel;

        private CardDeck _deck;
        private bool _hasNetworkRemaining;
        private int _networkRemaining;

        public void SetNetworkRemaining(int remaining)
        {
            remaining = Mathf.Max(0, remaining);
            if (_hasNetworkRemaining && _networkRemaining == remaining) return;
            _hasNetworkRemaining = true;
            _networkRemaining = remaining;
            Refresh();
        }

        public void SetDeck(CardDeck deck)
        {
            if (_deck != null)
            {
                _deck.OnDiscarded -= Refresh;
                _deck.OnCardDrawn -= OnCardDrawn;
                _deck.OnDeckReshuffled -= Refresh;
            }
            _deck = deck;
            _hasNetworkRemaining = false;
            if (_deck != null)
            {
                _deck.OnDiscarded += Refresh;
                _deck.OnCardDrawn += OnCardDrawn;
                _deck.OnDeckReshuffled += Refresh;
            }
        }

        private void OnCardDrawn(Card card) => Refresh();

        public void Refresh()
        {
            if (_deck == null) { Debug.LogWarning("[CardCounter] _deck 为空"); return; }
            if (cells == null || cells.Length == 0) { Debug.LogWarning("[CardCounter] cells 数组为空"); return; }

#if UNITY_EDITOR
            ValidateCells();
#endif

            var counter = new Dictionary<CardRank, int>();
            foreach (var card in _deck.DiscardPile)
            {
                counter.TryGetValue(card.Rank, out int count);
                counter[card.Rank] = count + 1;
            }

            foreach (var cell in cells)
            {
                int discarded = counter.GetValueOrDefault(cell.rank, 0);
                int total = _deck.GetTotalPerRank(cell.rank);
                int remaining = Mathf.Max(0, total - discarded); // 兜底：联机模式 Master/Client 牌堆不同步时防止负值
                bool exhausted = remaining <= 0;

                if (cell.countText != null)
                {
                    cell.countText.text = $"{remaining}/{total}";
                    cell.countText.color = exhausted ? Color.red : Color.white;
                }
                if (cell.background != null)
                    cell.background.color = exhausted ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.8f, 0.8f, 0.8f);
                if (cell.brokenIndicator != null)
                    cell.brokenIndicator.SetActive(exhausted);
            }

            if (deckRemainingLabel != null)
                deckRemainingLabel.text = $"牌堆: {(_hasNetworkRemaining ? _networkRemaining : _deck.NetworkRemaining)}";
            else
                Debug.LogWarning("[CardCounter] deckRemainingLabel 为空");
        }

#if UNITY_EDITOR
        private bool _validated;
        private void ValidateCells()
        {
            if (_validated) return;
            _validated = true;
            var seen = new HashSet<CardRank>();
            foreach (var cell in cells)
            {
                if (cell.countText == null)
                    Debug.LogWarning($"[CardCounter] rank={cell.rank} 的 countText 未赋值");
                if (!seen.Add(cell.rank))
                    Debug.LogWarning($"[CardCounter] rank={cell.rank} 在 cells 数组中重复！会导致该点数数据冲突");
            }
        }
#endif

        private void OnDestroy()
        {
            if (_deck != null)
            {
                _deck.OnDiscarded -= Refresh;
                _deck.OnCardDrawn -= OnCardDrawn;
                _deck.OnDeckReshuffled -= Refresh;
            }
        }
    }
}
