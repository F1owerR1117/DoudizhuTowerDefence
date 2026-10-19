using System;
using System.Collections;
using System.Collections.Generic;
using DoudizhuTower.Config;
using DoudizhuTower.Gameplay.Fusion;
using DoudizhuTower.Gameplay.Network;
using DoudizhuTower.Gameplay.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Bidding
{
    /// <summary>
    /// 联机叫分控制器。
    /// 3 个真人玩家轮流叫分，通过网络同步。
    /// Master 客户端负责轮次管理和结果判定。
    /// </summary>
    public class NetworkBiddingManager : MonoBehaviour
    {
        [Header("计时与结果")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button confirmButton;

        [Header("玩家面板 — 自己（中间）")]
        [SerializeField] private TextMeshProUGUI playerLabelMe;
        [SerializeField] private TextMeshProUGUI bidDisplayMe;
        [SerializeField] private Image roleIconMe;

        [Header("玩家面板 — 左边")]
        [SerializeField] private TextMeshProUGUI playerLabelLeft;
        [SerializeField] private TextMeshProUGUI bidDisplayLeft;
        [SerializeField] private Image roleIconLeft;

        [Header("玩家面板 — 右边")]
        [SerializeField] private TextMeshProUGUI playerLabelRight;
        [SerializeField] private TextMeshProUGUI bidDisplayRight;
        [SerializeField] private Image roleIconRight;

        [Header("叫分按钮")]
        [SerializeField] private Button bid1Button;
        [SerializeField] private Button bid2Button;
        [SerializeField] private Button bid3Button;
        [SerializeField] private Button passButton;

        [Header("配置")]
        [SerializeField] private BiddingConfig biddingConfig;

        private INetworkService _net;
        private int _mySlot = -1;
        private int _myPlayerId;
        private int[] _actorNumbers;
        private int[] _bids = new int[3];
        private int _localTurnSlot;
        private int _highestBidder = -1;
        private int _highestBid;
        private bool _biddingEnded;
        private bool _initialized;
        private bool _shownResult;
        private System.Collections.Generic.HashSet<int> _aiSlots = new();

        // 左右两侧对应的网络槽位（-1 = 未分配）
        private int _leftSlot = -1;
        private int _rightSlot = -1;

        private void Start()
        {
            _net = NetworkFacade.Service;
            if (_net == null)
            {
                Debug.LogError("[NetworkBidding] 网络服务不可用");
                return;
            }

            // 等待 IdentityService 同步完成后再计算槽位
            StartCoroutine(InitializeSlotWhenReady());

            if (resultPanel != null) resultPanel.SetActive(false);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);

            if (bid1Button != null) bid1Button.onClick.AddListener(() => OnBid(1));
            if (bid2Button != null) bid2Button.onClick.AddListener(() => OnBid(2));
            if (bid3Button != null) bid3Button.onClick.AddListener(() => OnBid(3));
            if (passButton != null) passButton.onClick.AddListener(() => OnBid(0));

            // 隐藏角色图标
            SetRoleIconsActive(false);

            _net.OnCustomEvent += OnNetworkEvent;
            _net.OnPlayerLeft += OnPlayerLeft;
        }

        private IEnumerator InitializeSlotWhenReady()
        {
            // 等待 FusionGameManager 就绪
            float timeout = 5f;
            while (timeout > 0f)
            {
                if (FusionGameManager.Instance != null)
                    break;
                timeout -= 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            var gm = FusionGameManager.Instance;
            if (gm != null)
            {
                // 等待 Host 的 IdentityReady 锁释放
                float identityTimeout = 10f;
                while (identityTimeout > 0f)
                {
                    if (gm.HasStateAuthority || gm.World.Game.IdentityReady == 1)
                        break;
                    identityTimeout -= 0.1f;
                    yield return new WaitForSeconds(0.1f);
                }

                _mySlot = gm.GetLocalSlot();
                Debug.Log($"[NetworkBidding] Slot from FusionGameManager: mySlot={_mySlot}, HasStateAuth={gm.HasStateAuthority}, IdentityReady={gm.World.Game.IdentityReady}");
            }

            if (_mySlot < 0)
            {
                Debug.LogError("[NetworkBidding] 无法确定本机槽位，禁用叫分输入");
                SetButtonsInteractable(false);
                yield break;
            }

            // AI 槽位：Host 从 GameSession，Client 从 WorldState
            if (gm != null && !gm.HasStateAuthority)
            {
                _aiSlots.Clear();
                var world = gm.World;
                for (int i = 0; i < 3; i++)
                {
                    var p = gm.GetPlayerState(i);
                    if (p.IsAI == 1) _aiSlots.Add(i);
                }
                Debug.Log($"[NetworkBidding] Client AI 槽位: [{string.Join(",", _aiSlots)}]");
            }
            else
            {
                _aiSlots = new System.Collections.Generic.HashSet<int>(GameSession.AISlots);
                Debug.Log($"[NetworkBidding] Host AI 槽位: [{string.Join(",", _aiSlots)}]");
            }

            AssignSideSlots();
            SetPlayerLabels();
            _initialized = true;

            UpdateBidButtons();
            UpdateTurnDisplay();

            Debug.Log($"[NetworkBidding] 初始化完成，本机槽位={_mySlot}");
        }

        /// <summary>
        /// 将大厅原始 AI 槽位（playerSlots[] 索引）转换为 actor-number 排序后的槽位索引。
        /// 必须在 PlayerList 同步完成后调用（_actorNumbers 已就绪）。
        /// </summary>
        private void ConvertAISlots()
        {
            // Phase 5 Final：AI slot 由 LobbyIdentityService 管理
        }

        private void OnEnable()
        {
            GameSession.OnRuntimeReset += ResetRuntime;
        }

        private void OnDisable()
        {
            GameSession.OnRuntimeReset -= ResetRuntime;
        }

        private void ResetRuntime()
        {
            _biddingEnded = false;
            _initialized = false;
        }

        private void OnDestroy()
        {
            GameSession.OnRuntimeReset -= ResetRuntime;
            if (_net != null)
            {
                _net.OnCustomEvent -= OnNetworkEvent;
                _net.OnPlayerLeft -= OnPlayerLeft;
            }
        }

        private void Update()
        {
            var gm = FusionGameManager.Instance;
            if (gm == null) return;

            // Client 等待 Host 身份初始化完成
            if (!gm.HasStateAuthority && gm.World.Game.IdentityReady == 0) return;

            var world = gm.World;

            // ── 倒计时：从 WorldState 读取（Host 写入，Client 同步） ──
            int stateStartTick = world.Game.StateStartTick;
            int elapsed = gm.Runner.Tick - stateStartTick;
            int remaining = Mathf.Max(0, gm.BiddingDurationTicks - elapsed);
            int seconds = Mathf.CeilToInt(remaining / 100f);
            if (timerText != null)
                timerText.text = $"{seconds}s";

            // ── 叫分已结束 → 显示结果 ──
            if (world.Game.IsBiddingFinished == 1 && !_shownResult)
            {
                _shownResult = true;

                int landlordSlot = world.Game.BidWinnerSlot;
                bool localIsLandlord = (landlordSlot == _mySlot);
                float multiplier = world.Game.HighestBid > 0 ? world.Game.HighestBid : 1f;

                SetButtonsInteractable(false);
                ShowResult(localIsLandlord, multiplier);
                ShowRoleIcons(landlordSlot);

                if (confirmButton != null)
                {
                    confirmButton.gameObject.SetActive(true);
                    confirmButton.interactable = true;
                }

                Debug.Log($"[NetworkBidding] 从 WorldState 读取叫分结果: landlord={landlordSlot}, multiplier={multiplier}");
            }

            // ── 叫分进行中 → 更新轮次 + 叫分显示 + 按钮状态 ──
            if (world.Game.IsBiddingFinished == 0)
            {
                int currentTurn = world.Game.CurrentBidTurn;
                var labels = new[] {
                    GetLabelForSlot(0),
                    GetLabelForSlot(1),
                    GetLabelForSlot(2)
                };
                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i] != null)
                        labels[i].fontStyle = i == currentTurn
                            ? FontStyles.Bold
                            : FontStyles.Normal;
                }

                for (int slot = 0; slot < 3; slot++)
                {
                    var player = gm.GetPlayerState(slot);
                    var bidText = GetBidDisplayForSlot(slot);
                    if (bidText != null)
                    {
                        string display = player.Bid > 0 ? $"{player.Bid} 分" : "不叫";
                        if (bidText.text != display)
                        {
                            bidText.text = display;
                            Debug.Log($"[BidDisplay] slot={slot} IsAI={player.IsAI} Bid={player.Bid} → {display}");
                        }
                    }
                }

                UpdateBidButtons();
            }
        }

        /// <summary>
        /// 从 WorldState 读取叫分轮次，更新 UI。
        /// </summary>
        private void UpdateTurnDisplayFromWorld(WorldState world)
        {
            if (world.Game.Phase != 0) return;

            int currentTurn = world.Game.CurrentBidTurn;
            var labels = new[] {
                GetLabelForSlot(0),
                GetLabelForSlot(1),
                GetLabelForSlot(2)
            };

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null)
                    labels[i].fontStyle = i == currentTurn
                        ? FontStyles.Bold
                        : FontStyles.Normal;
            }
        }

        /// <summary>
        /// 从 WorldState 读取叫分结果，更新 UI。
        /// </summary>
        private void UpdateBidDisplayFromWorld(WorldState world)
        {
            if (world.Game.Phase != 0) return;

            for (int slot = 0; slot < 3; slot++)
            {
                var player = GetPlayerFromWorld(world, slot);
                var bidText = GetBidDisplayForSlot(slot);
                if (bidText != null)
                    bidText.text = player.Bid > 0 ? $"{player.Bid} 分" : "不叫";
            }
        }

        private PlayerState GetPlayerFromWorld(WorldState world, int slot)
        {
            switch (slot)
            {
                case 0: return world.Player0;
                case 1: return world.Player1;
                case 2: return world.Player2;
                default: return world.Player0;
            }
        }

        // ─── 玩家叫分 ───

        private void OnBid(int bid)
        {
            if (_biddingEnded) return;

            var gm = FusionGameManager.Instance;
            if (gm != null)
            {
                int mySlot = gm.GetLocalSlot();
                if (mySlot < 0 || mySlot >= 3) return;

                // 轮次校验：不是自己的回合则拒绝
                if (gm.World.Game.CurrentBidTurn != mySlot) return;

                if (gm.HasStateAuthority)
                {
                    // Host 直接提交
                    gm.SubmitBid(mySlot, bid);
                    Debug.Log($"[Bidding] Host bid: slot={mySlot} bid={bid}");
                }
                else
                {
                    // Client 通过 RPC 发送到 Host
                    gm.RpcSubmitBid(mySlot, bid);
                    Debug.Log($"[Bidding] Client RPC bid: slot={mySlot} bid={bid}");
                }
            }
            else
            {
                Debug.Log($"[Bidding] Local bid: slot={_mySlot} bid={bid}");
                ProcessBid(_mySlot, bid);
            }

            SetButtonsInteractable(false);
        }

        // ─── 网络事件处理 ───

        private void OnNetworkEvent(string key, object value, int senderActor)
        {
            // Fusion 模式：从 WorldState 读取，不处理网络事件
            if (FusionGameManager.Instance != null) return;
        }

        private void ProcessBid(int slot, int bid)
        {
            // 叫分场景桥接：本地处理叫分逻辑
            if (slot != _localTurnSlot) return;

            _bids[slot] = bid;

            var bidText = GetBidDisplayForSlot(slot);
            if (bidText != null)
                bidText.text = bid > 0 ? $"{bid} 分" : "不叫";

            if (bid > _highestBid)
            {
                _highestBid = bid;
                _highestBidder = slot;
            }

            int maxBid = biddingConfig != null ? biddingConfig.maxBid : 3;

            if (bid == maxBid)
            {
                MasterEndBidding();
                return;
            }

            int nextTurn = _localTurnSlot + 1;
            if (nextTurn >= 3)
            {
                if (_highestBidder >= 0)
                {
                    MasterEndBidding();
                    return;
                }
                nextTurn = 0;
            }

            _localTurnSlot = nextTurn;

            UpdateBidButtons();
            UpdateTurnDisplay();
        }

        // ─── 结束叫分（仅 Master）───

        private void MasterEndBidding()
        {
            // 叫分场景桥接：本地处理叫分结果
            int landlordSlot;
            float multiplier;

            if (_highestBidder >= 0)
            {
                landlordSlot = _highestBidder;
                multiplier = _highestBid;
            }
            else
            {
                landlordSlot = UnityEngine.Random.Range(0, 3);
                multiplier = 1f;
            }

            int seed = Environment.TickCount;

            GameSession.SetNetworkMode(true);
            GameSession.NetworkSeed = seed;
            GameSession.LandlordSlot = landlordSlot;
            GameSession.BidMultiplier = multiplier;
            GameSession.HasResult = true;
            GameSession.SetLocalPlayerIsLandlord(landlordSlot == _mySlot);

            SetButtonsInteractable(false);

            bool localIsLandlord = (landlordSlot == _mySlot);
            ShowResult(localIsLandlord, multiplier);
            ShowRoleIcons(landlordSlot);

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = true;
            }

            _biddingEnded = true;
        }

        private void OnConfirm()
        {
            var gm = FusionGameManager.Instance;
            if (gm != null && gm.HasStateAuthority)
            {
                gm.ConfirmBidding();
            }
            GameSession.SetNetworkMode(true);
            if (gm != null)
            {
                var world = gm.World;
                int landlordSlot = world.Game.BidWinnerSlot;
                float multiplier = world.Game.HighestBid > 0 ? world.Game.HighestBid : 1f;
                GameSession.SetResultNetwork(landlordSlot, multiplier);
                GameSession.SetLocalPlayerIsLandlord(landlordSlot == _mySlot);
            }
            _net.LoadScene(SceneLoader.GAME_SCENE);
        }

        private void OnPlayerLeft(string playerName)
        {
            if (_biddingEnded) return;

            int realPlayerCount = _net.CurrentPlayerCount;
            int totalSlots = realPlayerCount + _aiSlots.Count;

            if (totalSlots < 3)
            {
                _biddingEnded = true;
                SetButtonsInteractable(false);
                if (resultText != null)
                    resultText.text = "有玩家断线，叫分取消";
                if (resultPanel != null) resultPanel.SetActive(true);
                Debug.Log("[NetworkBidding] 玩家断线，叫分取消");
            }
        }

        // ─── 槽位分配 ───

        private void AssignSideSlots()
        {
            int side = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i == _mySlot) continue;
                if (side == 0) _leftSlot = i;
                else _rightSlot = i;
                side++;
            }
        }

        // ─── UI 更新 ───

        private TextMeshProUGUI GetLabelForSlot(int slot)
        {
            if (slot == _mySlot) return playerLabelMe;
            if (slot == _leftSlot) return playerLabelLeft;
            if (slot == _rightSlot) return playerLabelRight;
            return null;
        }

        private TextMeshProUGUI GetBidDisplayForSlot(int slot)
        {
            if (slot == _mySlot) return bidDisplayMe;
            if (slot == _leftSlot) return bidDisplayLeft;
            if (slot == _rightSlot) return bidDisplayRight;
            return null;
        }

        private Image GetRoleIconForSlot(int slot)
        {
            if (slot == _mySlot) return roleIconMe;
            if (slot == _leftSlot) return roleIconLeft;
            if (slot == _rightSlot) return roleIconRight;
            return null;
        }

        /// <summary>根据 slot 索引获取对应的 PlayerId（actorNumber）</summary>
        private int GetPlayerIdForSlot(int slot)
        {
            if (_actorNumbers != null && slot >= 0 && slot < _actorNumbers.Length)
                return _actorNumbers[slot];
            return -1;
        }

        private void SetPlayerLabels()
        {
            if (_net == null) return;
            string[] names = _net.GetPlayerNames();

            if (playerLabelMe != null)
            {
                if (_aiSlots.Contains(_mySlot))
                    playerLabelMe.text = "AI（你）";
                else
                    playerLabelMe.text = _mySlot < names.Length ? $"{names[_mySlot]}（你）" : "你";
            }

            if (playerLabelLeft != null && _leftSlot >= 0)
            {
                if (_aiSlots.Contains(_leftSlot))
                    playerLabelLeft.text = $"<color=#FFD700>AI-{_leftSlot + 1}</color>";
                else
                    playerLabelLeft.text = _leftSlot < names.Length ? names[_leftSlot] : "等待中...";
            }

            if (playerLabelRight != null && _rightSlot >= 0)
            {
                if (_aiSlots.Contains(_rightSlot))
                    playerLabelRight.text = $"<color=#FFD700>AI-{_rightSlot + 1}</color>";
                else
                    playerLabelRight.text = _rightSlot < names.Length ? names[_rightSlot] : "等待中...";
            }
        }

        private void UpdateBidButtons()
        {
            if (_biddingEnded)
            {
                SetButtonsInteractable(false);
                return;
            }

            // Phase 5 Final：从 WorldState 读取状态
            var gm = FusionGameManager.Instance;
            if (gm == null) return;

            var world = gm.World;
            bool isMyTurn = world.Game.CurrentBidTurn == _mySlot;
            int maxBid = biddingConfig != null ? biddingConfig.maxBid : 3;

            if (bid1Button != null) bid1Button.interactable = isMyTurn && world.Game.HighestBid < 1 && maxBid >= 1;
            if (bid2Button != null) bid2Button.interactable = isMyTurn && world.Game.HighestBid < 2 && maxBid >= 2;
            if (bid3Button != null) bid3Button.interactable = isMyTurn && world.Game.HighestBid < 3 && maxBid >= 3;
            if (passButton != null) passButton.interactable = isMyTurn;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (bid1Button != null) bid1Button.interactable = interactable;
            if (bid2Button != null) bid2Button.interactable = interactable;
            if (bid3Button != null) bid3Button.interactable = interactable;
            if (passButton != null) passButton.interactable = interactable;
        }

        private void UpdateTurnDisplay()
        {
            // Phase 5 Final：从 WorldState 读取
            var gm = FusionGameManager.Instance;
            int currentTurn = (gm != null) ? gm.World.Game.CurrentBidTurn : 0;

            var labels = new[] {
                GetLabelForSlot(0),
                GetLabelForSlot(1),
                GetLabelForSlot(2)
            };

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null)
                    labels[i].fontStyle = i == currentTurn
                        ? FontStyles.Bold
                        : FontStyles.Normal;
            }
        }

        private void SetRoleIconsActive(bool active)
        {
            if (roleIconMe != null) roleIconMe.gameObject.SetActive(active);
            if (roleIconLeft != null) roleIconLeft.gameObject.SetActive(active);
            if (roleIconRight != null) roleIconRight.gameObject.SetActive(active);
        }

        private void ShowRoleIcons(int landlordSlot)
        {
            var icons = new[] {
                GetRoleIconForSlot(0),
                GetRoleIconForSlot(1),
                GetRoleIconForSlot(2)
            };

            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                icons[i].gameObject.SetActive(true);
                icons[i].color = (i == landlordSlot) ? Color.red : Color.green;
            }
        }

        private void ShowResult(bool localIsLandlord, float multiplier)
        {
            Debug.Log($"[NetworkBidding] ShowResult called: localIsLandlord={localIsLandlord}, multiplier={multiplier}, resultPanel={resultPanel}, resultText={resultText}");
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
                Debug.Log($"[NetworkBidding] resultPanel activated: {resultPanel.name}, active={resultPanel.activeSelf}");
            }
            else
            {
                Debug.LogError("[NetworkBidding] resultPanel is NULL! 请在 Inspector 中赋值");
            }
            if (resultText != null)
            {
                string role = localIsLandlord ? "地主" : "农民";
                resultText.text = $"你是 {role}\n叫分倍数: x{multiplier:F0}";
            }
        }

        // ─── AI 叫分逻辑 ───

        private int AIDecideBid()
        {
            int minBid = _highestBid > 0 ? _highestBid + 1 : 1;
            int maxBid = biddingConfig != null ? biddingConfig.maxBid : 3;

            if (minBid > maxBid) return 0;

            float passChance = biddingConfig != null ? biddingConfig.aiPassChance : 0.6f;
            if (UnityEngine.Random.value < passChance) return 0;

            float w1 = biddingConfig != null ? biddingConfig.aiBid1Weight : 0.5f;
            float w2 = biddingConfig != null ? biddingConfig.aiBid2Weight : 0.3f;
            float w3 = biddingConfig != null ? biddingConfig.aiBid3Weight : 0.2f;

            float total = 0f;
            if (minBid <= 1) total += w1;
            if (minBid <= 2) total += w2;
            if (minBid <= 3) total += w3;

            if (total <= 0f) return 0;

            float roll = UnityEngine.Random.value * total;
            if (minBid <= 1) { roll -= w1; if (roll <= 0) return 1; }
            if (minBid <= 2) { roll -= w2; if (roll <= 0) return 2; }
            return maxBid;
        }
    }
}
