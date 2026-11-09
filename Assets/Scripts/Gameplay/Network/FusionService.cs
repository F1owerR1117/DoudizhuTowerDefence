using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using DoudizhuTower.Gameplay.Systems;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// Fusion 2 网络服务实现。
    /// 最小闭环：Runner 启动 → 连接 → 分配 PlayerRef → Tick 运行。
    /// 不包含任何游戏逻辑。
    /// </summary>
    public class FusionService : MonoBehaviour, INetworkService, INetworkRunnerCallbacks
    {
        public static FusionService Instance { get; private set; }

        private NetworkRunner _runner;
        private bool _isConnected;
        private bool _isInRoom;
        private bool _isMaster;
        private string _playerName = "Player";
        private string _currentRoomName;

        [Header("Fusion Spawn")]
        [Tooltip("加入房间后自动 Spawn 的 NetworkObject Prefab（如 FusionTestObject）")]
        [SerializeField] private NetworkObject _spawnPrefab;

        /// <summary>Fusion NetworkRunner（外部可访问）</summary>
        public NetworkRunner Runner => _runner;

        /// <summary>本机 PlayerRef（Fusion 身份）</summary>
        public PlayerRef LocalPlayer { get; private set; }

        /// <summary>FusionGameManager 预制体（在 Inspector 中设置）</summary>
        [SerializeField] private NetworkObject _fusionGameManagerPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Build 中 TLS 证书验证修复：Photon 云 HTTPS 连接需要此回调
            ServicePointManager.ServerCertificateValidationCallback = CertCallback;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static bool CertCallback(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // 允许 Photon 云的自签名证书（Build 环境）
            Debug.LogWarning($"[Fusion] TLS 证书警告: {sslPolicyErrors}，已放行");
            return true;
        }

        // ─── INetworkService 实现 ───

        public void Connect()
        {
            if (this == null || gameObject == null) return;

            // 复用已有 Runner，或创建新 Runner
            if (_runner == null)
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
                Debug.Log("[Fusion] 创建新 Runner");
            }

            _runner.ProvideInput = true;

            // 只注册一次回调，防止重复注册
            _runner.RemoveCallbacks(this);
            _runner.AddCallbacks(this);

            if (!_isConnected)
            {
                _isConnected = true;
                Debug.Log($"[Fusion] Runner 就绪: ProvideInput={_runner.ProvideInput}");
                OnServerConnected?.Invoke();
            }
        }

        /// <summary>
        /// 通过 NetworkRunner Spawn FusionGameManager。
        /// 只有 Host/Master 可以 Spawn。
        /// </summary>
        public void SpawnFusionGameManager()
        {
            Debug.Log($"[Fusion] SpawnFusionGameManager 被调用: Runner={_runner != null}, IsRunning={_runner?.IsRunning}");

            if (_runner == null || !_runner.IsRunning)
            {
                Debug.LogError($"[Fusion] NetworkRunner 未运行！Runner={_runner}, IsRunning={_runner?.IsRunning}");
                return;
            }

            if (DoudizhuTower.Gameplay.Fusion.FusionGameManager.Instance != null)
            {
                Debug.Log("[Fusion] FusionGameManager 已存在，跳过 Spawn");
                return;
            }

            Debug.Log($"[Fusion] _fusionGameManagerPrefab={_fusionGameManagerPrefab != null}");

            // 方式 1：使用预制体（推荐）
            if (_fusionGameManagerPrefab != null)
            {
                Debug.Log("[Fusion] 尝试通过 Runner.SpawnAsync 预制体...");
                _ = SpawnGameManagerAsync();
                return;
            }

            // 方式 2：运行时创建（添加到 Runner 所在 GameObject）
            Debug.LogWarning("[Fusion] 未配置 FusionGameManager 预制体，尝试运行时创建");
            var existingNO = gameObject.GetComponent<global::Fusion.NetworkObject>();
            if (existingNO == null)
            {
                existingNO = gameObject.AddComponent<global::Fusion.NetworkObject>();
            }
            var gm = gameObject.AddComponent<DoudizhuTower.Gameplay.Fusion.FusionGameManager>();
            Debug.Log($"[Fusion] 运行时创建 FusionGameManager: Instance={DoudizhuTower.Gameplay.Fusion.FusionGameManager.Instance != null}");
        }

        private async Task SpawnGameManagerAsync()
        {
            try
            {
                var obj = await _runner.SpawnAsync(_fusionGameManagerPrefab);
                Debug.Log($"[Fusion] SpawnAsync 完成: {obj != null}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Fusion] SpawnAsync 失败: {e.Message}");
            }
        }

        public void Disconnect()
        {
            ShutdownRunner();
            _isConnected = false;
            _isInRoom = false;
            UpdateMasterRole(false);
            _currentRoomName = null;
            LocalPlayer = default;
        }

        /// <summary>
        /// 安全关闭 Runner。
        /// 不调用 Destroy —— Destroy 会使 Runner 组件被标记销毁，
        /// 但 Fusion 内部的回调循环仍持有引用，导致每帧 "runner should not" 警告。
        /// 只调用 Shutdown() 让 Runner 回到可复用状态。
        /// </summary>
        private void ShutdownRunner()
        {
            if (_runner != null)
            {
                _runner.RemoveCallbacks(this);
                if (_runner.IsRunning)
                {
                    _runner.Shutdown();
                }
                // 不调用 Destroy(_runner) —— 保留 Runner 组件以便复用
                _runner = null;
            }
        }

        public bool IsConnected => _isConnected;

        public async void CreateRoom(string roomCode, int maxPlayers)
        {
            if (this == null || gameObject == null) return;
            Debug.Log($"[Fusion] CreateRoom 开始: roomCode={roomCode}, runner={_runner != null}");
            if (_runner == null) Connect();
            if (this == null || gameObject == null) return;

            try
            {
                var args = new StartGameArgs
                {
                    GameMode = GameMode.Host,
                    SessionName = roomCode,
                    PlayerCount = maxPlayers,
                    SceneManager = GetOrCreateSceneManager()
                };
                Debug.Log($"[Fusion] StartGame 调用中... GameMode={args.GameMode}, Session={args.SessionName}");

                var result = await _runner.StartGame(args);

                Debug.Log($"[Fusion] StartGame 结果: Ok={result.Ok}, ErrorMessage={result.ErrorMessage}");

                if (result.Ok)
                {
                    _isInRoom = true;
                    UpdateMasterRole(true);
                    _currentRoomName = roomCode;
                    LocalPlayer = _runner.LocalPlayer;
                    Debug.Log($"[Fusion] Host 已创建房间: {roomCode}, PlayerRef={LocalPlayer}, RunnerState={_runner.State}");
                    SpawnTestObject();
                    OnRoomCreateSuccess?.Invoke(roomCode);
                }
                else
                {
                    Debug.LogError($"[Fusion] 创建房间失败: Ok={result.Ok}, Error={result.ErrorMessage}");
                    OnRoomJoinError?.Invoke(result.ErrorMessage);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Fusion] CreateRoom 异常: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public async void JoinRoom(string roomCode)
        {
            if (this == null || gameObject == null) return;
            Debug.Log($"[Fusion] JoinRoom 开始: roomCode={roomCode}, runner={_runner != null}");
            if (_runner == null) Connect();
            if (this == null || gameObject == null) return;

            try
            {
                var args = new StartGameArgs
                {
                    GameMode = GameMode.Client,
                    SessionName = roomCode,
                    SceneManager = GetOrCreateSceneManager()
                };
                Debug.Log($"[Fusion] StartGame 调用中... GameMode={args.GameMode}, Session={args.SessionName}");

                var result = await _runner.StartGame(args);

                Debug.Log($"[Fusion] StartGame 结果: Ok={result.Ok}, ErrorMessage={result.ErrorMessage}");

                if (result.Ok)
                {
                    _isInRoom = true;
                    UpdateMasterRole(false);
                    _currentRoomName = roomCode;
                    LocalPlayer = _runner.LocalPlayer;
                    Debug.Log($"[Fusion] Client 已加入房间: {roomCode}, PlayerRef={LocalPlayer}, RunnerState={_runner.State}");
                    OnRoomJoinSuccess?.Invoke(roomCode);
                }
                else
                {
                    Debug.LogError($"[Fusion] 加入房间失败: Ok={result.Ok}, Error={result.ErrorMessage}");
                    OnRoomJoinError?.Invoke(result.ErrorMessage);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Fusion] JoinRoom 异常: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public async void JoinRandomRoom()
        {
            if (this == null || gameObject == null) return;
            if (_runner == null) Connect();
            if (this == null || gameObject == null) return;

            try
            {
                var result = await _runner.StartGame(new StartGameArgs
                {
                    GameMode = GameMode.Client,
                    SceneManager = GetOrCreateSceneManager()
                });

                if (this == null || gameObject == null) return;

                if (result.Ok)
                {
                    _isInRoom = true;
                    UpdateMasterRole(false);
                    _currentRoomName = _runner.SessionInfo.Name;
                    LocalPlayer = _runner.LocalPlayer;
                    Debug.Log($"[Fusion] 已随机加入房间: {_currentRoomName}, PlayerRef={LocalPlayer}");
                    OnRoomJoinSuccess?.Invoke(_currentRoomName);
                }
                else
                {
                    Debug.LogError($"[Fusion] 随机加入失败: {result}");
                    OnRoomJoinError?.Invoke(result.ToString());
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Fusion] JoinRandomRoom 异常: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>获取或创建 NetworkSceneManagerDefault</summary>
        private NetworkSceneManagerDefault GetOrCreateSceneManager()
        {
            var sceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>();
            if (sceneManager == null)
            {
                sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
                Debug.Log("[Fusion] 创建 NetworkSceneManagerDefault");
            }
            return sceneManager;
        }

        /// <summary>Spawn 测试对象（仅 Server/Host 执行）</summary>
        private void SpawnTestObject()
        {
            if (_runner == null || !_runner.IsServer) return;
            if (_spawnPrefab == null)
            {
                Debug.LogWarning("[Fusion] _spawnPrefab 未设置，跳过 Spawn");
                return;
            }
            _runner.Spawn(_spawnPrefab, Vector3.zero, Quaternion.identity);
            Debug.Log("[Fusion] 测试对象已 Spawn");
        }

        public void LeaveRoom()
        {
            ShutdownRunner();
            _isInRoom = false;
            UpdateMasterRole(false);
            _currentRoomName = null;
            LocalPlayer = default;
            // 重置网络模式锁，允许下次切换到单机模式
            GameSession.ResetNetworkModeLock();
            GameSession.AISlots.Clear();
            GameSession.RawAISlots.Clear();
        }

        public bool IsInRoom => _isInRoom;
        public bool IsMasterClient => _isMaster;
        public string CurrentRoomName => _currentRoomName ?? "";
        public int CurrentPlayerCount => _runner != null ? System.Linq.Enumerable.Count(_runner.ActivePlayers) : 0;
        public int MaxPlayers => 3;

        public string LocalPlayerName
        {
            get => _playerName;
            set => _playerName = value;
        }

        public string[] GetPlayerNames()
        {
            if (_runner == null) return Array.Empty<string>();
            var list = new System.Collections.Generic.List<(int raw, string name)>();
            foreach (var p in _runner.ActivePlayers)
            {
                string name = _runner.IsServer && p == _runner.LocalPlayer
                    ? "Host (你)"
                    : $"Player_{p.RawEncoded}";
                list.Add((p.RawEncoded, name));
            }
            list.Sort((a, b) => a.raw.CompareTo(b.raw));
            var result = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                result[i] = list[i].name;
            return result;
        }

        private readonly System.Collections.Generic.HashSet<int> _readyPlayers = new();

        public void SetPlayerReady(bool ready)
        {
            if (_runner == null) return;
            int raw = _runner.LocalPlayer.RawEncoded;
            bool changed = ready ? _readyPlayers.Add(raw) : _readyPlayers.Remove(raw);
            if (!changed) return;

            OnPlayerReadyChanged?.Invoke();
            if (AreAllPlayersReady)
                OnAllPlayersReady?.Invoke();
        }

        public bool AreAllPlayersReady
        {
            get
            {
                if (_runner == null) return false;
                foreach (var p in _runner.ActivePlayers)
                {
                    if (!_readyPlayers.Contains(p.RawEncoded)) return false;
                }
                return _readyPlayers.Count > 0;
            }
        }

        [System.Obsolete] public void SendToAll(string key, object value) { }
        [System.Obsolete] public void SendToMaster(string key, object value) { }
        [System.Obsolete] public void SendToPlayer(int actorNumber, string key, object value) { }

        public void SetRoomProperty(string key, object value) { }
        public object GetRoomProperty(string key) => null;

        public void LoadScene(string sceneName)
        {
            if (_runner != null && _runner.IsServer)
                _runner.LoadScene(sceneName);
        }

        public int LocalActorNumber => LocalPlayer.RawEncoded;
        public int[] GetPlayerActorNumbers()
        {
            if (_runner == null) return Array.Empty<int>();
            var list = new System.Collections.Generic.List<int>();
            foreach (var p in _runner.ActivePlayers)
                list.Add(p.RawEncoded);
            list.Sort();
            return list.ToArray();
        }

        public int GetActorNumberAtPosition(int position)
        {
            var actors = GetPlayerActorNumbers();
            if (position < 0 || position >= actors.Length) return -1;
            return actors[position];
        }

        // ─── 事件 ───

        public event Action OnServerConnected;
        public event Action OnConnectionLost;
        public event Action<string> OnRoomCreateSuccess;
        public event Action<string> OnRoomJoinSuccess;
        public event Action<string> OnRoomJoinError;
        public event Action<string> OnPlayerJoined;
        public event Action<string> OnPlayerLeft;
        public event Action OnAllPlayersReady;
        public event Action OnPlayerReadyChanged;
        public event Action OnMasterSwitched;

        // Retained for the shared service interface. Fusion's current transport uses WorldState
        // and does not publish arbitrary custom events.
        private Action<string, object, int> _legacyCustomEventHandlers;
        public event Action<string, object, int> OnCustomEvent
        {
            add => _legacyCustomEventHandlers += value;
            remove => _legacyCustomEventHandlers -= value;
        }

        private void UpdateMasterRole(bool isMaster)
        {
            if (_isMaster == isMaster) return;
            _isMaster = isMaster;
            OnMasterSwitched?.Invoke();
        }

        // ─── INetworkRunnerCallbacks ───

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[Fusion] 玩家加入: PlayerRef={player}");
            UpdateMasterRole(runner.IsServer);
            OnPlayerJoined?.Invoke($"Player_{player.RawEncoded}");

            // 转发到 FusionGameManager（slot 分配）
            var gm = FindAnyObjectByType<DoudizhuTower.Gameplay.Fusion.FusionGameManager>();
            if (gm != null) gm.OnPlayerJoinedSlot(player);
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[Fusion] 玩家离开: PlayerRef={player}");
            UpdateMasterRole(runner.IsServer);
            OnPlayerLeft?.Invoke($"Player_{player.RawEncoded}");

            // 转发到 FusionGameManager（slot 清理）
            var gm = FindAnyObjectByType<DoudizhuTower.Gameplay.Fusion.FusionGameManager>();
            if (gm != null) gm.OnPlayerLeftSlot(player);
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("[Fusion] 已连接到服务器");
            _isConnected = true;
            OnServerConnected?.Invoke();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"[Fusion] 断开连接: {reason}");
            _isInRoom = false;
            OnConnectionLost?.Invoke();
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"[Fusion] Runner 关闭: {shutdownReason}");
            _isInRoom = false;
            UpdateMasterRole(false);
            LocalPlayer = default;
        }

        // ─── 未实现的回调（占位）───
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason reason)
        {
            Debug.LogError($"[Fusion] 连接失败: Address={address}, Reason={reason}");
            _isConnected = false;
            OnConnectionLost?.Invoke();
        }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var gm = DoudizhuTower.Gameplay.Fusion.FusionGameManager.Instance;
            if (gm != null && gm.TryGetLocalInput(out var localInput))
            {
                input.Set(localInput);
            }
        }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnReadyToStart(NetworkRunner runner) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectSpawned(NetworkRunner runner, NetworkObject obj) { }
        public void OnObjectDespawned(NetworkRunner runner, NetworkObject obj) { }
        public void OnFailedToConnectPeerToServer(NetworkRunner runner, PlayerRef player, NetDisconnectReason reason) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
