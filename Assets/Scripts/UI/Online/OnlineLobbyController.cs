using System.Collections.Generic;
using DoudizhuTower.Gameplay.Network;
using DoudizhuTower.Gameplay.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Online
{
    /// <summary>
    /// 联机大厅控制器。
    /// 通过 INetworkService 接口与网络层交互，不直接依赖 Photon。
    /// </summary>
    public class OnlineLobbyController : MonoBehaviour
    {
        [Header("面板")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject roomPanel;
        [SerializeField] private GameObject matchmakingPanel;

        [Header("大厅面板")]
        [SerializeField] private Button soloQueueButton;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private Button backToMenuButton;

        [Header("房间面板")]
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private TextMeshProUGUI[] playerSlots;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveRoomButton;
        [SerializeField] private TextMeshProUGUI roomStatusText;

        [Header("AI 与踢人")]
        [SerializeField] private Button addAIButton;
        [SerializeField] private Button[] kickButtons; // 每个玩家槽位旁的踢出按钮

        [Header("匹配面板")]
        [SerializeField] private TextMeshProUGUI matchmakingStatusText;
        [SerializeField] private TextMeshProUGUI matchmakingTimerText;
        [SerializeField] private Button cancelMatchButton;

        private INetworkService _net;
        private bool _isReady;
        private bool _isMatching;
        private bool _isInLobby;
        private float _matchTimeout;
        private const float MATCH_TIMEOUT = 30f;

        private HashSet<int> _aiSlots = new HashSet<int>();
        private int _mySlot = -1;

        private void Start()
        {
            // 使用 NetworkFacade 获取网络服务
            Debug.Log($"[OnlineLobby] NetworkFacade.Service = {NetworkFacade.Service}");
            _net = NetworkFacade.Service;
            Debug.Log($"[OnlineLobby] IsConnected = {_net?.IsConnected}");

            ShowLobby();

            // 大厅按钮
            if (soloQueueButton != null) soloQueueButton.onClick.AddListener(OnSoloQueue);
            if (createRoomButton != null) createRoomButton.onClick.AddListener(OnCreateRoom);
            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnJoinRoom);
            if (backToMenuButton != null) backToMenuButton.onClick.AddListener(OnBackToMenu);

            // 房间按钮
            if (readyButton != null) readyButton.onClick.AddListener(OnToggleReady);
            if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGame);
            if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(OnLeaveRoom);
            if (addAIButton != null) addAIButton.onClick.AddListener(OnAddAI);

            // 踢出按钮
            if (kickButtons != null)
            {
                for (int i = 0; i < kickButtons.Length; i++)
                {
                    int slot = i; // 闭包捕获
                    if (kickButtons[i] != null)
                        kickButtons[i].onClick.AddListener(() => OnKickPlayer(slot));
                }
            }

            // 匹配按钮
            if (cancelMatchButton != null) cancelMatchButton.onClick.AddListener(OnCancelMatch);

            // 订阅网络事件
            if (_net != null)
            {
                _net.OnServerConnected += OnServerConnected;
                _net.OnRoomCreateSuccess += OnRoomCreateSuccess;  // 添加这行
                _net.OnRoomJoinSuccess += OnRoomJoinSuccess;
                _net.OnRoomJoinError += OnRoomJoinError;
                _net.OnPlayerJoined += OnPlayerJoined;
                _net.OnPlayerLeft += OnPlayerLeft;
                _net.OnAllPlayersReady += OnAllPlayersReady;
                _net.OnPlayerReadyChanged += OnPlayerReadyChanged;
                _net.OnConnectionLost += OnConnectionLost;
                _net.OnCustomEvent += OnCustomEvent;

                // 连接服务器
                _net.Connect();
            }
            else
            {
                Debug.LogWarning("[OnlineLobby] NetworkManager 未找到，请确保场景中有 NetworkManager");
            }
        }

        private void OnDestroy()
        {
            if (_net != null)
            {
                _net.OnServerConnected -= OnServerConnected;
                _net.OnRoomCreateSuccess -= OnRoomCreateSuccess;  // 添加这行
                _net.OnRoomJoinSuccess -= OnRoomJoinSuccess;
                _net.OnRoomJoinError -= OnRoomJoinError;
                _net.OnPlayerJoined -= OnPlayerJoined;
                _net.OnPlayerLeft -= OnPlayerLeft;
                _net.OnAllPlayersReady -= OnAllPlayersReady;
                _net.OnPlayerReadyChanged -= OnPlayerReadyChanged;
                _net.OnConnectionLost -= OnConnectionLost;
                _net.OnCustomEvent -= OnCustomEvent;
            }
        }

        private void Update()
        {
            // 持续检测连接就绪状态（OnConnectedToMaster 可能在客户端未完全就绪时触发）
            if (!_isInLobby && _net != null && _net.IsConnected && !_net.IsInRoom)
            {
                _isInLobby = true;
                UpdateLobbyButtons();
            }

            if (!_isMatching) return;

            _matchTimeout -= Time.deltaTime;
            if (matchmakingTimerText != null)
            {
                int sec = Mathf.CeilToInt(Mathf.Max(0f, _matchTimeout));
                matchmakingTimerText.text = $"{sec}s";
            }

            if (_net != null && matchmakingStatusText != null)
            {
                int count = _net.IsInRoom ? _net.CurrentPlayerCount : 1;
                matchmakingStatusText.text = $"正在匹配... ({count}/3)";
            }

            if (_matchTimeout <= 0f)
            {
                _isMatching = false;
                if (_net != null && _net.IsInRoom) _net.LeaveRoom();
                ShowLobby();
                Debug.Log("[OnlineLobby] 匹配超时");
            }
        }

        #region 面板切换

        private void ShowLobby()
        {
            SetPanelActive(lobbyPanel);
            _isMatching = false;
            UpdateLobbyButtons();
        }

        private void ShowRoom()
        {
            SetPanelActive(roomPanel);
            _isReady = false;
            UpdateRoomUI();
        }

        private void ShowMatchmaking()
        {
            SetPanelActive(matchmakingPanel);
            _isMatching = true;
            _matchTimeout = MATCH_TIMEOUT;
            if (matchmakingStatusText != null)
                matchmakingStatusText.text = "正在匹配... (1/3)";
            if (matchmakingTimerText != null)
                matchmakingTimerText.text = $"{MATCH_TIMEOUT}s";
        }

        private void SetPanelActive(GameObject activePanel)
        {
            if (lobbyPanel != null) lobbyPanel.SetActive(activePanel == lobbyPanel);
            if (roomPanel != null) roomPanel.SetActive(activePanel == roomPanel);
            if (matchmakingPanel != null) matchmakingPanel.SetActive(activePanel == matchmakingPanel);
        }

        #endregion

        #region 大厅操作

        private void UpdateLobbyButtons()
        {
            if (soloQueueButton != null) soloQueueButton.interactable = _isInLobby;
            if (createRoomButton != null) createRoomButton.interactable = _isInLobby;
            if (joinRoomButton != null) joinRoomButton.interactable = _isInLobby;
        }

        private void OnSoloQueue()
        {
            if (_net == null) return;
            _net.JoinRandomRoom();
            ShowMatchmaking();
        }

        private void OnCreateRoom()
        {
            if (_net == null) return;
            string code = GenerateRoomCode();
            _net.CreateRoom(code, 3);
        }

        private void OnJoinRoom()
        {
            if (_net == null) return;
            string code = roomCodeInput != null ? roomCodeInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code))
            {
                Debug.Log("[OnlineLobby] 请输入房间号");
                return;
            }
            _net.JoinRoom(code);
        }

        private void OnBackToMenu()
        {
            if (_net != null && _net.IsInRoom)
                _net.LeaveRoom();
            // 不调用 Disconnect()，保留 NetworkManager
            _isInLobby = false;
            GameSession.Reset();
            SceneLoader.LoadMainMenu();
        }

        #endregion

        #region 房间操作

        private void UpdateRoomUI()
        {
            if (_net == null) return;

            string[] names = _net.GetPlayerNames();
            int totalCount = _net.CurrentPlayerCount + _aiSlots.Count;

            for (int i = 0; i < playerSlots.Length; i++)
            {
                if (playerSlots[i] == null) continue;
                if (_aiSlots.Contains(i))
                    playerSlots[i].text = $"<color=#FFD700>AI-{i + 1}</color>";
                else if (i < names.Length)
                    playerSlots[i].text = names[i];
                else
                    playerSlots[i].text = "<color=#888888>等待中...</color>";
            }

            // 踢出按钮：仅房主可见，不能踢自己（房主始终在大厅位置 0），可踢真人和 AI
            if (kickButtons != null)
            {
                for (int i = 0; i < kickButtons.Length; i++)
                {
                    if (kickButtons[i] == null) continue;
                    bool hasTarget = _aiSlots.Contains(i) || i < names.Length;
                    bool canKick = _net.IsMasterClient && i != 0 && hasTarget;
                    kickButtons[i].gameObject.SetActive(canKick);
                }
            }

            // 添加 AI 按钮：仅房主可见，人数 + AI < 3 时可点击
            if (addAIButton != null)
            {
                addAIButton.gameObject.SetActive(_net.IsMasterClient);
                addAIButton.interactable = totalCount < 3;
            }

            if (roomCodeText != null)
                roomCodeText.text = $"房间号: {_net.CurrentRoomName}";

            if (readyButton != null)
            {
                var text = readyButton.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = _isReady ? "取消准备" : "准备";
            }

            if (startGameButton != null)
                startGameButton.interactable = _net.IsMasterClient && totalCount >= 3;

            if (roomStatusText != null)
            {
                if (totalCount < 3)
                    roomStatusText.text = $"等待玩家加入... ({totalCount}/3)";
                else
                    roomStatusText.text = "可以开始游戏";
            }
        }

        private void OnToggleReady()
        {
            if (_net == null) return;
            _isReady = !_isReady;
            _net.SetPlayerReady(_isReady);
            UpdateRoomUI();
        }

        private void OnStartGame()
        {
            if (_net == null || !_net.IsMasterClient) return;
            int totalCount = _net.CurrentPlayerCount + _aiSlots.Count;
            if (totalCount < 3) return;

            // 房主同步跳转到叫分场景
            GameSession.Reset();
            GameSession.SetNetworkMode(true);
            GameSession.AISlots = new HashSet<int>(_aiSlots);
            GameSession.RawAISlots = new HashSet<int>(_aiSlots);

            _net.LoadScene(SceneLoader.BIDDING_SCENE);
        }

        private void OnLeaveRoom()
        {
            if (_net != null && _net.IsInRoom)
                _net.LeaveRoom();
            _isReady = false;
            _aiSlots.Clear();
            ShowLobby();
        }

        #endregion

        #region 匹配操作

        private void OnCancelMatch()
        {
            _isMatching = false;
            if (_net != null && _net.IsInRoom)
                _net.LeaveRoom();
            ShowLobby();
        }

        #endregion

        #region 网络回调

        private void OnServerConnected()
        {
            _isInLobby = true;
            UpdateLobbyButtons();
            Debug.Log("[OnlineLobby] 已连接到服务器");
        }

        private void OnConnectionLost()
        {
            _isMatching = false;
            _isInLobby = false;
            ShowLobby();
            Debug.Log("[OnlineLobby] 已断开连接");
        }

        private void OnRoomCreateSuccess(string roomName)
        {
            _isMatching = false;
            _isInLobby = false;

            // 计算本机槽位（房主是第一个玩家）
            _mySlot = 0;

            if (roomCodeText != null)
                roomCodeText.text = $"房间号: {roomName}";
            ShowRoom();
            Debug.Log($"[OnlineLobby] 房间创建成功: {roomName}");
        }

        private void OnRoomJoinSuccess(string roomName)
        {
            _isMatching = false;
            _isInLobby = false;

            // 计算本机槽位
            if (_net != null)
            {
                var actors = _net.GetPlayerActorNumbers();
                _mySlot = NetworkProtocol.GetPlayerSlot(_net.LocalActorNumber, actors);
            }

            // 从 FusionGameManager 读取 AI 槽位（WorldState 自动同步）
            SyncAIFromWorldState();

            if (roomCodeText != null)
                roomCodeText.text = $"房间号: {roomName}";
            ShowRoom();
        }

        private void OnRoomJoinError(string message)
        {
            if (_isMatching)
            {
                // 匹配失败（无可用房间），自动创建房间等待别人加入
                Debug.Log("[OnlineLobby] 未找到可用房间，自动创建房间等待匹配");
                string code = GenerateRoomCode();
                _net.CreateRoom(code, 3);
                return;
            }

            _isMatching = false;
            ShowLobby();
            Debug.LogWarning($"[OnlineLobby] 加入房间失败: {message}");
        }

        private void OnPlayerJoined(string playerName)
        {
            UpdateRoomUI();
        }

        private void OnPlayerLeft(string playerName)
        {
            UpdateRoomUI();
        }

        private void OnAllPlayersReady()
        {
            UpdateRoomUI();
        }

        private void OnPlayerReadyChanged()
        {
            UpdateRoomUI();
        }

        #endregion

        #region AI 与踢人

        private void OnAddAI()
        {
            if (_net == null || !_net.IsMasterClient) return;
            int totalCount = _net.CurrentPlayerCount + _aiSlots.Count;
            if (totalCount >= 3) return;

            // 找到第一个空槽位
            int targetSlot = -1;
            string[] names = _net.GetPlayerNames();
            for (int i = 0; i < 3; i++)
            {
                if (i >= names.Length && !_aiSlots.Contains(i))
                {
                    targetSlot = i;
                    break;
                }
            }

            if (targetSlot < 0)
            {
                // 无空槽位，找一个 AI 还没占的槽位
                for (int i = 0; i < 3; i++)
                {
                    if (!_aiSlots.Contains(i) && i < names.Length)
                    {
                        // 这个槽位有真人，跳过
                        continue;
                    }
                    if (!_aiSlots.Contains(i))
                    {
                        targetSlot = i;
                        break;
                    }
                }
            }

            if (targetSlot < 0) return;

            // 通过 FusionGameManager 修改 WorldState（自动同步）
            var gm = DoudizhuTower.Gameplay.Fusion.FusionGameManager.Instance;
            if (gm != null)
            {
                gm.AddAISlot(targetSlot);
            }
            ApplyAddAI(targetSlot);
        }

        private void ApplyAddAI(int slot)
        {
            _aiSlots.Add(slot);
            UpdateRoomUI();
            Debug.Log($"[OnlineLobby] 添加 AI 到槽位 {slot}, AISlots=[{string.Join(",", _aiSlots)}]");
        }

        /// <summary>从 WorldState 同步 AI 槽位到本地 UI</summary>
        private void SyncAIFromWorldState()
        {
            _aiSlots.Clear();
            var gm = DoudizhuTower.Gameplay.Fusion.FusionGameManager.Instance;
            if (gm == null) return;

            for (int i = 0; i < 3; i++)
            {
                if (gm.IsAISlotByState(i))
                    _aiSlots.Add(i);
            }
        }

        private void OnKickPlayer(int slot)
        {
            if (_net == null || !_net.IsMasterClient) return;
            if (_aiSlots.Contains(slot))
            {
                // 移除 AI：通过 FusionGameManager 修改 WorldState
                var gm = DoudizhuTower.Gameplay.Fusion.FusionGameManager.Instance;
                if (gm != null)
                {
                    gm.RemoveAISlot(slot);
                }
                ApplyRemoveAI(slot);
            }
            else
            {
                // 踢真人（将大厅位置转换为 actor number 后发送）
                int actorNumber = _net.GetActorNumberAtPosition(slot);
                if (actorNumber > 0 && _net is LocalNetworkService localService)
                    localService.SendToPlayer(actorNumber, NetworkProtocol.KICK_PLAYER, 0);
            }
        }

        private void ApplyRemoveAI(int slot)
        {
            _aiSlots.Remove(slot);
            UpdateRoomUI();
            Debug.Log($"[OnlineLobby] 移除 AI 槽位 {slot}");
        }

        private void OnCustomEvent(string key, object value, int senderActor)
        {
            switch (key)
            {
                case NetworkProtocol.KICK_PLAYER:
                    Debug.Log("[OnlineLobby] 你被房主踢出房间");
                    _aiSlots.Clear();
                    if (_net != null && _net.IsInRoom)
                        _net.LeaveRoom();
                    ShowLobby();
                    break;
            }
        }

        #endregion

        private string GenerateRoomCode()
        {
            return Random.Range(100000, 999999).ToString();
        }
    }
}
