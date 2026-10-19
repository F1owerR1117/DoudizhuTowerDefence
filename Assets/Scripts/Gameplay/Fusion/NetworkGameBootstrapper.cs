using System;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Core.Economy;
using DoudizhuTower.Config;
using DoudizhuTower.Gameplay.Battle;
using DoudizhuTower.Gameplay.Entities;
using DoudizhuTower.Gameplay.Network;
using DoudizhuTower.Gameplay.Systems;
using DoudizhuTower.UI.Battlefield;
using DoudizhuTower.UI.Hand;
using DoudizhuTower.UI.HUD;
using DoudizhuTower.UI.Panels;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 联机模式游戏场景初始化器。
    /// 从 GameBootstrapper 分离所有 Fusion 相关逻辑，
    /// 由 BiddingSceneBootstrap 在联机模式下激活。
    /// </summary>
    public class NetworkGameBootstrapper : MonoBehaviour
    {
        [Header("手牌")]
        [SerializeField] private HandArea handArea;
        [SerializeField] private CardCounterUI cardCounter;

        [Header("战斗")]
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private DoudizhuTower.Gameplay.Systems.EconomyManager economyManager;
        [SerializeField] private GameStateMachine gameStateMachine;
        [SerializeField] private TimerQueue timerQueue;

        [Header("UI")]
        [SerializeField] private LaunchTubeUI launchTubeUI;
        [SerializeField] private TempSlotUI tempSlotUI;
        [SerializeField] private TempSlotUI teammateTempSlotUI;
        [SerializeField] private GameObject laneArea;
        [SerializeField] private UnityEngine.UI.Button drawButton;
        [SerializeField] private DomainSystem domainSystem;
        [SerializeField] private DomainUIController domainUIController;
        [SerializeField] private UnityEngine.UI.Button domainButton;
        [SerializeField] private PauseMenu pauseMenu;
        [SerializeField] private VictoryPanel victoryPanel;

        [Header("基地")]
        [SerializeField] private Component[] baseBuildings;

        private FusionGameManager _gm;
        private CardDeck _mainDeck;
        private CardHand _playerHand;
        private bool _playerIsLandlord;
        private int _mySlot;
        private Component[] _slotBaseBuildings;
        private Action<Card[], CardTypeResult, RouteGroup> _playRequestHandler;
        private UnityEngine.Events.UnityAction _drawClickHandler;
        private UnityEngine.Events.UnityAction _domainClickHandler;
        private Action<Card> _transferHandler;
        private bool _networkEndShown;

        public CardDeck MainDeck => _mainDeck;
        public CardHand PlayerHand => _playerHand;
        public bool IsLandlord => _playerIsLandlord;

        private void Awake()
        {
            if (GameSession.HasResult)
            {
                _playerIsLandlord = GameSession.PlayerIsLandlord;
            }
        }

        private IEnumerator Start()
        {
            yield return UIManager.WaitForReady();

            while (this != null && (_gm = FusionGameManager.Instance) == null)
                yield return null;

            while (this != null && (_gm.Runner == null || _gm.World.Game.IdentityReady == 0
                   || _gm.GetLocalSlot() < 0 || !_gm.IsMatchReady || _gm.State != GamePhase.Playing))
                yield return null;

            if (this == null || _gm == null) yield break;
            _mySlot = _gm.GetLocalSlot();
            if (_mySlot < 0 || _mySlot >= 3)
            {
                Debug.LogError($"[NetworkGameBootstrapper] 本机槽位无效: {_mySlot}");
                yield break;
            }

            var localPlayerState = _gm.GetPlayerState(_mySlot);
            _playerIsLandlord = localPlayerState.IsLandlord == 1;
            CardUnit.PlayerIsLandlord = _playerIsLandlord;

            _slotBaseBuildings = MapBasesToPlayerSlots(baseBuildings);
            _gm.ConfigureGameplayScene(battleManager, _slotBaseBuildings);

            if (_slotBaseBuildings != null)
            {
                for (int slot = 0; slot < _slotBaseBuildings.Length && slot < 3; slot++)
                {
                    var building = _slotBaseBuildings[slot];
                    var cardUnit = building != null ? building.GetComponent<CardUnit>() : null;
                    if (cardUnit == null) continue;
                    cardUnit.OwnerPlayerId = slot;
                    cardUnit.SetLandlord(_gm.GetPlayerState(slot).IsLandlord == 1);
                    if (cardUnit._isBuilding)
                        cardUnit.InitBuildingHP(cardUnit.MaxHP > 0 ? cardUnit.MaxHP : 1000f);
                    var healthBar = cardUnit.GetComponentInChildren<UnitHealthBar>(true);
                    if (healthBar != null) healthBar.Initialize(cardUnit);
                }
            }

            // ── 联机模式：注入 BattleManager 依赖（GameBootstrapper 联机模式 yield break 跳过了这些） ──

            // 注入 baseBuildings 到 BattleManager
            if (battleManager != null && _slotBaseBuildings != null)
            {
                battleManager.SetBaseBuildings(_slotBaseBuildings);
                battleManager.Initialize();
                if (_gm.HasStateAuthority)
                    battleManager.OnGameEnded += _gm.ReportGameEnded;
                Debug.Log("[NetworkGameBootstrapper] BattleManager 注入完成");
            }

            // 先禁用所有本地 BuildingAI，稍后只在 Host 上为 AI 槽位绑定权威状态镜像。
            if (_slotBaseBuildings != null)
            {
                foreach (var bldg in _slotBaseBuildings)
                {
                    if (bldg == null) continue;
                    var ai = bldg.GetComponent<BuildingAI>();
                    if (ai != null) ai.enabled = false;
                }
            }

            // 演出系统
            var presentationMgr = DoudizhuTower.Gameplay.Presentation.BattlePresentationManager.Instance;
            if (presentationMgr != null && battleManager != null)
            {
                presentationMgr.OnPresentationStart += () => battleManager.IsPresentationActive = true;
                presentationMgr.OnPresentationEnd += () => battleManager.IsPresentationActive = false;
            }

            // 静态状态清理
            UnitAudio.ClearClipCounts();
            DamageQueue.Clear();
            CardUnit.SetBatchDamageEnabled(true);

            // 本地牌堆只用于把 Host 下发的稳定 DeckIndex 还原为 Card 对象。
            int seed = _gm.World.Game.Seed;
            _mainDeck = new CardDeck(seed);
            int handCapacity = _playerIsLandlord ? 20 : 17;
            _playerHand = new CardHand(handCapacity);
            ConfigureNetworkAIs();

            _gm.OnLocalHandSynchronized += ApplyLocalHandSnapshot;
            _gm.OnCardsPlayed += OnCardsPlayed;
            _gm.RequestLocalHandSnapshot();
            while (this != null && !_gm.HasHandSnapshot(_mySlot))
                yield return null;
            if (this == null) yield break;
            ApplyLocalHandSnapshot(_gm.GetHandCardsArray(_mySlot));

            // 手牌 UI
            int maxPlay = _playerIsLandlord ? 6 : 5;
            var playerBase = GetPlayerBase();
            var routeGroup = playerBase?.GetComponent<RouteGroup>();
            handArea.Initialize(_playerHand, maxPlay, routeGroup);
            ApplyRoleSpecificUI();
            handArea.SetDeck(_mainDeck);
            if (cardCounter != null)
            {
                cardCounter.SetDeck(_mainDeck);
                cardCounter.SetNetworkRemaining(_gm.World.Game.DeckCount);
            }

            _playRequestHandler = (cards, result, requestedRouteGroup) =>
            {
                if (cards == null || cards.Length == 0 || _gm == null) return;
                var indices = new byte[cards.Length];
                for (int i = 0; i < cards.Length; i++)
                    indices[i] = (byte)cards[i].DeckIndex;
                int routeIndex = requestedRouteGroup != null ? requestedRouteGroup.CurrentIndex : 0;
                _gm.SubmitPlayCards(_mySlot, indices, routeIndex, _mySlot);
            };
            handArea.OnPlayRequest += _playRequestHandler;

            if (economyManager == null)
                economyManager = FindFirstObjectByType<DoudizhuTower.Gameplay.Systems.EconomyManager>();

            SetupTransferTube();

            // 摸牌 RPC
            SetupDrawButton();

            // 领域按钮
            SetupDomainButton();

            // 暂停菜单
            SetupPauseMenu();

            // 胜利面板
            SetupVictoryPanel();

            // Host 等待所有真人完成场景绑定后才推进自动摸牌与操作请求。
            _gm.MarkLocalSceneReady();

            Debug.Log($"[NetworkGameBootstrapper] 初始化完成: slot={_mySlot}, landlord={_playerIsLandlord}, handCount={_playerHand.Count}");
        }

        private void ApplyLocalHandSnapshot(byte[] cardIndices)
        {
            if (_playerHand == null || _mainDeck == null || cardIndices == null) return;
            _playerHand.CardsList.Clear();
            foreach (byte id in cardIndices)
            {
                var card = _mainDeck.GetCardByIndex(id);
                if (card.DeckIndex >= 0) _playerHand.Add(card);
            }
            _playerHand.NotifyHandModified();
            handArea?.NotifyHandChanged();
            cardCounter?.Refresh();
        }

        private void ConfigureNetworkAIs()
        {
            if (_slotBaseBuildings == null) return;

            for (int slot = 0; slot < _slotBaseBuildings.Length && slot < 3; slot++)
            {
                var building = _slotBaseBuildings[slot];
                var ai = building != null ? building.GetComponent<BuildingAI>() : null;
                if (ai == null) continue;

                var player = _gm.GetPlayerState(slot);
                if (!_gm.HasStateAuthority || player.IsAI != 1 || !_gm.HasHandSnapshot(slot))
                {
                    ai.enabled = false;
                    continue;
                }

                int capacity = player.IsLandlord == 1 ? 20 : 17;
                var hand = new CardHand(capacity);
                foreach (byte id in _gm.GetHandCardsArray(slot))
                {
                    var card = _mainDeck.GetCardByIndex(id);
                    if (card.DeckIndex >= 0) hand.Add(card);
                }

                var economy = new EconomySystem(player.Gold, player.IncomeRate);
                float drawInterval = player.IsLandlord == 1 ? 5f : 6f;
                ai.InitializeNetwork(hand, economy, battleManager, _mainDeck,
                    player.IsLandlord == 1 ? 6 : 5, drawInterval, _gm, slot);
                ai.enabled = true;
                Debug.Log($"[NetworkGameBootstrapper] Host AI 已绑定权威状态：slot={slot}, hand={hand.Count}");
            }
        }

        private void OnCardsPlayed(int slot, byte[] cardIndices)
        {
            if (_mainDeck == null || cardIndices == null) return;
            var cards = new List<Card>(cardIndices.Length);
            foreach (byte id in cardIndices)
            {
                var card = _mainDeck.GetCardByIndex(id);
                if (card.DeckIndex >= 0) cards.Add(card);
            }
            if (cards.Count > 0) _mainDeck.Discard(cards);
            if (cardCounter != null)
            {
                cardCounter.SetNetworkRemaining(_gm.World.Game.DeckCount);
                cardCounter.Refresh();
            }
        }

        private void SetupDrawButton()
        {
            if (drawButton == null) return;

            _drawClickHandler = () =>
            {
                if (_playerHand.IsFull) { handArea?.ShowHandFullFeedback(); return; }
                _gm?.SubmitDrawCard();
            };
            drawButton.onClick.AddListener(_drawClickHandler);
        }

        private void SetupTransferTube()
        {
            if (launchTubeUI == null || _playerIsLandlord) return;
            launchTubeUI.Initialize(handArea);
            _transferHandler = card =>
            {
                if (card.DeckIndex >= 0 && card.DeckIndex < 54)
                    _gm?.SubmitTransfer(_mySlot, (byte)card.DeckIndex);
            };
            launchTubeUI.OnCardTransmitted += _transferHandler;
        }

        private void Update()
        {
            if (_gm == null || !_gm.IsMatchReady) return;
            var player = _gm.GetPlayerState(_mySlot);
            economyManager?.RenderNetworkState(player.Gold, player.IncomeRate);
            cardCounter?.SetNetworkRemaining(_gm.World.Game.DeckCount);
            var game = _gm.World.Game;
            if (domainSystem != null)
            {
                float tickDuration = _gm.Runner != null ? _gm.Runner.DeltaTime : Time.deltaTime;
                float domainRemaining = Mathf.Max(0, game.DomainEndTick - (_gm.Runner != null ? _gm.Runner.Tick : 0)) * tickDuration;
                float counterRemaining = Mathf.Max(0, game.CounterEndTick - (_gm.Runner != null ? _gm.Runner.Tick : 0)) * tickDuration;
                var domainType = new CardTypeResult { Type = (CardType)game.DomainType, MainRank = (CardRank)game.DomainMainRank, Length = game.DomainLength };
                var counterType = new CardTypeResult { Type = (CardType)game.CounterType, MainRank = (CardRank)game.CounterMainRank };
                domainSystem.ApplyNetworkSnapshot(game.DomainActive == 1, domainType,
                    game.DomainSlot >= 0 && game.DomainSlot < 3 && _gm.GetPlayerState(game.DomainSlot).IsLandlord == 1,
                    domainRemaining, game.DomainPending == 1 && game.DomainSlot == _mySlot,
                    game.CounterActive == 1, counterType, counterRemaining,
                    game.CounterPending == 1 && !_playerIsLandlord);
            }
            if (game.GameOver == 1 && !_networkEndShown)
            {
                _networkEndShown = true;
                PauseMenu.IsGameOver = true;
                float tickRate = _gm.Runner != null ? _gm.Runner.DeltaTime : Time.deltaTime;
                var stats = new VictoryStats
                {
                    gameDuration = Mathf.Max(0, _gm.Runner != null ? _gm.Runner.Tick - game.MatchStartTick : 0) * tickRate,
                    identityBaseScore = _playerIsLandlord ? 100 : 50,
                    bidMultiplier = Mathf.Max(1, game.HighestBid),
                    gameStateCoefficient = 1f
                };
                victoryPanel?.Show((game.WinnerIsLandlord == 1) == _playerIsLandlord, true, stats);
            }
        }

        private void SetupDomainButton()
        {
            if (domainButton == null) return;
            domainButton.gameObject.SetActive(_playerIsLandlord);
            _domainClickHandler = () =>
            {
                if (_gm != null)
                    _gm.SubmitDomain(_mySlot);
            };
            domainButton.onClick.AddListener(_domainClickHandler);

            if (domainUIController == null)
                domainUIController = FindFirstObjectByType<DomainUIController>();
            domainSystem?.Initialize(handArea, null, _playerIsLandlord);
            if (domainUIController != null)
            {
                domainUIController.Initialize(domainSystem, _playerIsLandlord);
                domainUIController.OnCounterButtonClicked += OnCounterButtonClicked;
            }
        }

        private void OnCounterButtonClicked()
        {
            if (_gm == null || _playerIsLandlord) return;
            _gm.SubmitDomain(_mySlot, true);
        }

        private void SetupPauseMenu()
        {
            if (pauseMenu == null) pauseMenu = UIManager.Instance?.PauseMenu;
            if (pauseMenu != null)
            {
                pauseMenu.SetMultiplayerMode(true);
                pauseMenu.OnQuitRequested += OnQuitToLobby;
            }
        }

        private void SetupVictoryPanel()
        {
            if (victoryPanel == null) victoryPanel = UIManager.Instance?.VictoryPanel;
            victoryPanel?.ResetForNewGame();
            PauseMenu.IsGameOver = false;

            if (victoryPanel != null)
            {
                victoryPanel.OnReturnToMenuRequested += SceneLoader.LoadMainMenu;
            }
        }

        private Component GetPlayerBase()
        {
            if (_slotBaseBuildings != null && _mySlot >= 0 && _mySlot < _slotBaseBuildings.Length)
                return _slotBaseBuildings[_mySlot];
            return null;
        }

        private Component[] MapBasesToPlayerSlots(Component[] sceneBases)
        {
            if (sceneBases == null || sceneBases.Length < 3) return sceneBases;

            int landlordSlot = -1;
            for (int slot = 0; slot < 3; slot++)
                if (_gm.GetPlayerState(slot).IsLandlord == 1) { landlordSlot = slot; break; }

            if (landlordSlot < 0)
            {
                Debug.LogError("[NetworkGameBootstrapper] 找不到地主槽位，无法映射主堡");
                return sceneBases;
            }

            Component landlordBase = null;
            var farmerBases = new List<Component>(2);
            foreach (var candidate in sceneBases)
            {
                var cardUnit = candidate != null ? candidate.GetComponent<CardUnit>() : null;
                if (cardUnit == null) continue;
                if (cardUnit.IsLandlord && landlordBase == null) landlordBase = candidate;
                else farmerBases.Add(candidate);
            }

            if (landlordBase == null || farmerBases.Count < 2)
            {
                Debug.LogError("[NetworkGameBootstrapper] 场景需要一座地主主堡和两座农民主堡；保留 Inspector 原顺序");
                return sceneBases;
            }

            var mapped = new Component[3];
            mapped[landlordSlot] = landlordBase;
            int farmerIndex = 0;
            for (int slot = 0; slot < 3; slot++)
                if (slot != landlordSlot) mapped[slot] = farmerBases[farmerIndex++];
            return mapped;
        }

        private void ApplyRoleSpecificUI()
        {
            bool isFarmer = !_playerIsLandlord;
            if (launchTubeUI != null) launchTubeUI.gameObject.SetActive(isFarmer);
            if (laneArea != null) laneArea.SetActive(_playerIsLandlord);
            handArea.SetRouteUIVisible(_playerIsLandlord);
            // 联机传牌由 Host 验证后直接加入队友手牌，不使用单机版本地暂存槽。
            if (tempSlotUI != null) tempSlotUI.gameObject.SetActive(false);
            if (teammateTempSlotUI != null) teammateTempSlotUI.gameObject.SetActive(false);
        }

        private void OnQuitToLobby()
        {
            NetworkFacade.LeaveRoom();
            SceneLoader.LoadOnlineLobby();
        }

        private void OnDestroy()
        {
            if (_gm != null)
            {
                _gm.OnLocalHandSynchronized -= ApplyLocalHandSnapshot;
                _gm.OnCardsPlayed -= OnCardsPlayed;
            }
            if (handArea != null && _playRequestHandler != null)
                handArea.OnPlayRequest -= _playRequestHandler;
            if (drawButton != null && _drawClickHandler != null)
                drawButton.onClick.RemoveListener(_drawClickHandler);
            if (domainButton != null && _domainClickHandler != null)
                domainButton.onClick.RemoveListener(_domainClickHandler);
            if (launchTubeUI != null && _transferHandler != null)
                launchTubeUI.OnCardTransmitted -= _transferHandler;
            if (pauseMenu != null)
                pauseMenu.OnQuitRequested -= OnQuitToLobby;
            if (domainUIController != null)
                domainUIController.OnCounterButtonClicked -= OnCounterButtonClicked;
            if (victoryPanel != null)
                victoryPanel.OnReturnToMenuRequested -= SceneLoader.LoadMainMenu;
            if (_gm != null && _gm.HasStateAuthority && battleManager != null)
                battleManager.OnGameEnded -= _gm.ReportGameEnded;
        }
    }
}
