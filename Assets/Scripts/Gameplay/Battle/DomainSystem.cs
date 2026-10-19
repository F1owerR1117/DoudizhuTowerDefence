using System;
using System.Collections.Generic;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.UI.Hand;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    /// <summary>
    /// 领域系统（partial class）：管理要不起领域（地主）和反制护盾（农民）。
    ///
    /// DomainSystem.cs           — 字段、属性、事件、初始化、生命周期、配置
    /// DomainSystem.Gameplay.cs  — 出牌触发、计时器、激活/关闭、手牌封印
    /// </summary>
    public partial class DomainSystem : MonoBehaviour
    {
        #region 配置参数

        [Header("-- 要不起领域（地主技能） --")]
        [Tooltip("要不起领域持续时间（秒）")]
        [SerializeField] private float domainDuration = 5f;

        [Tooltip("要不起领域冷却时间（秒）")]
        [SerializeField] private float domainCooldown = 30f;

        [Header("-- 反制护盾（农民技能） --")]
        [Tooltip("反制护盾持续时间（秒）")]
        [SerializeField] private float counterShieldDuration = 2f;

        [Tooltip("反制护盾冷却时间（秒）")]
        [SerializeField] private float counterShieldCooldown = 45f;

        #endregion

        #region 事件

        /// <summary>要不起领域激活事件（牌型，持续时间）</summary>
        public event Action<CardTypeResult, float> OnDomainActivated;

        /// <summary>反制护盾激活事件（牌型，持续时间）</summary>
        public event Action<CardTypeResult, float> OnCounterShieldActivated;

        /// <summary>要不起领域关闭事件</summary>
        public event Action OnDomainDeactivated;

        /// <summary>反制护盾关闭事件</summary>
        public event Action OnCounterShieldDeactivated;

        #endregion

        #region 运行时状态

        // 要不起领域状态
        private bool _isDomainActive;
        private bool _isDomainPending;
        private bool _domainPendingByLandlord;
        private bool _domainByLandlord;
        private float _domainTimer;
        private float _domainCooldownTimer;
        private CardTypeResult _currentDomainType;

        // 反制护盾状态
        private bool _isCounterShieldActive;
        private bool _isCounterPending;
        private bool _counterPendingByFarmer;
        private bool _playerClickedCounter;
        private float _counterShieldTimer;
        private float _counterShieldCooldownTimer;
        private CardTypeResult _currentCounterType;

        // 引用
        private HandArea _playerHandArea;
        private HandArea _enemyHandArea;
        private CardHand _playerCardHand;
        private CardHand _enemyCardHand;
        private List<CardHand> _allEnemyCardHands = new();
        private List<CardHand> _allFriendlyCardHands = new();
        private bool _isPlayerLandlord;
        private bool _networkDriven;

        #endregion

        #region 公共属性

        public bool IsDomainActive => _isDomainActive;
        public bool IsCounterShieldActive => _isCounterShieldActive;
        public bool IsDomainPending => _isDomainPending;
        public bool IsCounterPending => _isCounterPending;
        public bool IsDomainOnCooldown => _domainCooldownTimer > 0f;
        public bool IsCounterShieldOnCooldown => _counterShieldCooldownTimer > 0f;
        public float DomainCooldownRemaining => _domainCooldownTimer;
        public float CounterShieldCooldownRemaining => _counterShieldCooldownTimer;
        public float DomainTimeRemaining => _isDomainActive ? _domainTimer : 0f;
        public float CounterShieldTimeRemaining => _isCounterShieldActive ? _counterShieldTimer : 0f;
        public CardTypeResult CurrentCounterType => _currentCounterType;
        public CardTypeResult CurrentDomainType => _currentDomainType;

        public bool IsSealedByDomain(bool isPlayerLandlord)
        {
            if (!_isDomainActive) return false;
            bool isDomainOwner = isPlayerLandlord == _domainByLandlord;
            return !isDomainOwner;
        }

        public bool HasPlayerClickedCounter => _playerClickedCounter;

        public void SetPlayerClickedCounter()
        {
            _playerClickedCounter = true;
        }

        public void ApplyNetworkSnapshot(bool domainActive, CardTypeResult domainType, bool domainOwnerIsLandlord,
            float domainRemaining, bool domainPending, bool counterActive, CardTypeResult counterType,
            float counterRemaining, bool counterPending)
        {
            _networkDriven = true;
            _isDomainPending = domainPending;
            _domainPendingByLandlord = domainPending && _isPlayerLandlord;
            _isCounterPending = counterPending;
            _counterPendingByFarmer = counterPending;

            if (domainActive)
            {
                bool changed = !_isDomainActive || _currentDomainType.Type != domainType.Type
                    || _currentDomainType.MainRank != domainType.MainRank;
                if (changed) ActivateDomainInternal(domainType, domainOwnerIsLandlord);
                _domainTimer = Mathf.Max(0f, domainRemaining);
            }
            else if (_isDomainActive)
                DeactivateDomain();

            if (counterActive)
            {
                bool changed = !_isCounterShieldActive || _currentCounterType.Type != counterType.Type
                    || _currentCounterType.MainRank != counterType.MainRank;
                if (changed) ActivateCounterShieldInternal(counterType);
                _counterShieldTimer = Mathf.Max(0f, counterRemaining);
            }
            else if (_isCounterShieldActive)
                DeactivateCounterShield();
        }

        public float DomainDuration
        {
            get => domainDuration;
            set => domainDuration = Mathf.Max(0f, value);
        }

        public float DomainCooldown
        {
            get => domainCooldown;
            set => domainCooldown = Mathf.Max(0f, value);
        }

        public float CounterShieldDuration
        {
            get => counterShieldDuration;
            set => counterShieldDuration = Mathf.Max(0f, value);
        }

        public float CounterShieldCooldown
        {
            get => counterShieldCooldown;
            set => counterShieldCooldown = Mathf.Max(0f, value);
        }

        #endregion

        #region 初始化

        public void Initialize(HandArea playerHand, HandArea enemyHand, bool isPlayerLandlord = true)
        {
            _networkDriven = false;
            _playerHandArea = playerHand;
            _enemyHandArea = enemyHand;
            _isPlayerLandlord = isPlayerLandlord;

            if (_playerHandArea != null)
                _playerHandArea.OnHandChanged += OnPlayerHandChanged;
            if (_enemyHandArea != null)
                _enemyHandArea.OnHandChanged += OnEnemyHandChanged;
        }

        public void SetCardHands(CardHand playerCardHand, CardHand enemyCardHand)
        {
            _playerCardHand = playerCardHand;
            _enemyCardHand = enemyCardHand;

            if (enemyCardHand != null && !_allEnemyCardHands.Contains(enemyCardHand))
            {
                _allEnemyCardHands.Add(enemyCardHand);
                enemyCardHand.OnHandChanged += OnEnemyCardHandChanged;
            }
        }

        public void AddEnemyCardHand(CardHand enemyCardHand)
        {
            if (enemyCardHand == null) return;
            if (!_allEnemyCardHands.Contains(enemyCardHand))
            {
                _allEnemyCardHands.Add(enemyCardHand);
                enemyCardHand.OnHandChanged += OnEnemyCardHandChanged;
            }
        }

        public void AddFriendlyCardHand(CardHand friendlyCardHand)
        {
            if (friendlyCardHand == null) return;
            if (!_allFriendlyCardHands.Contains(friendlyCardHand))
            {
                _allFriendlyCardHands.Add(friendlyCardHand);
                friendlyCardHand.OnHandChanged += OnFriendlyCardHandChanged;
            }
        }

        private void OnFriendlyCardHandChanged(List<Card> _)
        {
            if (_isDomainActive && !_isPlayerLandlord)
            {
                foreach (var hand in _allFriendlyCardHands)
                    UpdateCardHandSealState(hand, _currentDomainType);
            }
        }

        private void OnEnemyCardHandChanged(List<Card> _)
        {
            if (_isDomainActive && _isPlayerLandlord)
            {
                foreach (var hand in _allEnemyCardHands)
                    UpdateCardHandSealState(hand, _currentDomainType);
            }
            if (_isCounterShieldActive && !_isPlayerLandlord)
            {
                foreach (var hand in _allEnemyCardHands)
                    UpdateCardHandSealState(hand, _currentCounterType);
            }
        }

        private void OnPlayerHandChanged()
        {
            if (_isDomainActive && !_isPlayerLandlord)
                UpdateHandSealState(_playerHandArea, _currentDomainType);
            if (_isCounterShieldActive && _isPlayerLandlord)
                UpdateHandSealState(_playerHandArea, _currentCounterType);
        }

        private void OnEnemyHandChanged()
        {
            if (_isDomainActive && _isPlayerLandlord)
                UpdateHandSealState(_enemyHandArea, _currentDomainType);
            if (_isCounterShieldActive && !_isPlayerLandlord)
                UpdateHandSealState(_enemyHandArea, _currentCounterType);
        }

        #endregion

        #region 生命周期

        private void OnDestroy()
        {
            if (_playerHandArea != null)
                _playerHandArea.OnHandChanged -= OnPlayerHandChanged;
            if (_enemyHandArea != null)
                _enemyHandArea.OnHandChanged -= OnEnemyHandChanged;

            foreach (var hand in _allEnemyCardHands)
                hand.OnHandChanged -= OnEnemyCardHandChanged;
            foreach (var hand in _allFriendlyCardHands)
                hand.OnHandChanged -= OnFriendlyCardHandChanged;
        }

        private void Update()
        {
            if (_networkDriven)
            {
                UpdateCooldowns();
                return;
            }
            UpdateDomainTimer();
            UpdateCounterShieldTimer();
            UpdateCooldowns();
        }

        #endregion

        #region 公共方法 - 按钮触发

        public void SetDomainPending(bool byLandlord = true)
        {
            if (_isCounterShieldActive)
            {
                Debug.LogWarning("[DomainSystem] 反制护盾生效中，无法开启领域");
                return;
            }

            if (_domainCooldownTimer > 0f)
            {
                Debug.LogWarning($"[DomainSystem] 要不起领域冷却中，剩余 {_domainCooldownTimer:F1}s");
                return;
            }

            _isDomainPending = true;
            _domainPendingByLandlord = byLandlord;
            _isCounterPending = false;
        }

        public void CancelDomainPending()
        {
            _isDomainPending = false;
            _domainPendingByLandlord = false;
        }

        public void SetCounterPending(bool byFarmer = true)
        {
            if (_counterShieldCooldownTimer > 0f)
            {
                Debug.LogWarning($"[DomainSystem] 反制护盾冷却中，剩余 {_counterShieldCooldownTimer:F1}s");
                return;
            }

            if (!_isDomainActive)
            {
                Debug.LogWarning("[DomainSystem] 当前没有要不起领域，无法反制");
                return;
            }

            _isCounterPending = true;
            _counterPendingByFarmer = byFarmer;
            _isDomainPending = false;
        }

        public void CancelPending()
        {
            _isDomainPending = false;
            _domainPendingByLandlord = false;
            _isCounterPending = false;
            _counterPendingByFarmer = false;
            _playerClickedCounter = false;
        }

        #endregion

        #region 公共方法 - 运行时修改配置

        public void SetDomainDuration(float duration) => domainDuration = Mathf.Max(0f, duration);
        public void SetDomainCooldown(float cooldown) => domainCooldown = Mathf.Max(0f, cooldown);
        public void SetCounterShieldDuration(float duration) => counterShieldDuration = Mathf.Max(0f, duration);
        public void SetCounterShieldCooldown(float cooldown) => counterShieldCooldown = Mathf.Max(0f, cooldown);
        public void ResetDomainCooldown() => _domainCooldownTimer = 0f;
        public void ResetCounterShieldCooldown() => _counterShieldCooldownTimer = 0f;
        public void ResetAllCooldowns() { _domainCooldownTimer = 0f; _counterShieldCooldownTimer = 0f; }

        #endregion
    }
}
