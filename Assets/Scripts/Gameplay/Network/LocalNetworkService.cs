using System;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// INetworkService 的本地模拟实现。
    /// 消息通过 LocalNetworkHub 直接方法调用传递，零网络延迟。
    /// 用于单进程多玩家测试，不需要 Photon。
    /// </summary>
    public class LocalNetworkService : INetworkService
    {
        private int _actorNumber;
        private string _playerName;
        private bool _isMaster;
        private bool _isConnected;
        private bool _isInRoom;

        public LocalNetworkService(string playerName)
        {
            _playerName = playerName;
        }

        /// <summary>初始化并注册到 Hub（必须在使用前调用）</summary>
        public void Initialize()
        {
            _actorNumber = LocalNetworkHub.Register(this);
            _isMaster = (_actorNumber == 1); // 第一个注册的为 Master
            _isConnected = true;
            _isInRoom = true;
            LocalNetworkHub.NotifyPlayerJoined(this);
            Debug.Log($"[LocalNet] {_playerName} 初始化: ActorNumber={_actorNumber}, IsMaster={_isMaster}");
        }

        /// <summary>从 Hub 注销</summary>
        public void Shutdown()
        {
            LocalNetworkHub.Unregister(this);
            _isConnected = false;
            _isInRoom = false;
        }

        /// <summary>接收消息（由 Hub 调用）</summary>
        public void ReceiveEvent(string key, object value, int senderActor)
        {
            OnCustomEvent?.Invoke(key, value, senderActor);
        }

        // ─── INetworkService 实现 ───

        public void Connect()
        {
            _isConnected = true;
            OnServerConnected?.Invoke();
        }

        public void Disconnect()
        {
            bool wasConnected = _isConnected;
            Shutdown();
            if (wasConnected)
                OnConnectionLost?.Invoke();
        }

        public bool IsConnected => _isConnected;

        public void CreateRoom(string roomCode, int maxPlayers)
        {
            _isInRoom = true;
            OnRoomCreateSuccess?.Invoke(roomCode);
        }

        public void JoinRoom(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                OnRoomJoinError?.Invoke("房间号不能为空");
                return;
            }
            _isInRoom = true;
            OnRoomJoinSuccess?.Invoke(roomCode);
        }

        public void JoinRandomRoom()
        {
            _isInRoom = true;
            OnRoomJoinSuccess?.Invoke("LocalRoom");
        }

        public void LeaveRoom()
        {
            _isInRoom = false;
        }

        public bool IsInRoom => _isInRoom;
        public bool IsMasterClient => _isMaster;
        public string CurrentRoomName => "LocalRoom";
        public int CurrentPlayerCount => LocalNetworkHub.PlayerCount;
        public int MaxPlayers => 4;

        public string LocalPlayerName
        {
            get => _playerName;
            set => _playerName = value;
        }

        public string[] GetPlayerNames() => LocalNetworkHub.GetAllPlayerNames();

        public void SetPlayerReady(bool ready) => LocalNetworkHub.SetPlayerReady(this, ready);

        public bool AreAllPlayersReady => LocalNetworkHub.AreAllPlayersReady;

        [System.Obsolete] public void SendToAll(string key, object value) { }
        [System.Obsolete] public void SendToMaster(string key, object value) { }
        public void SendToPlayer(int actorNumber, string key, object value) =>
            LocalNetworkHub.SendToPlayer(actorNumber, key, value, _actorNumber);

        public void SetRoomProperty(string key, object value) { }
        public object GetRoomProperty(string key) => null;

        public void LoadScene(string sceneName)
        {
            // 本地模式不实际加载场景（已在同一场景）
            Debug.Log($"[LocalNet] LoadScene({sceneName}) — 本地模式跳过");
        }

        public int LocalActorNumber => _actorNumber;
        public int[] GetPlayerActorNumbers() => LocalNetworkHub.GetAllActorNumbers();
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
        public event Action<string, object, int> OnCustomEvent;
        public event Action OnMasterSwitched;

        internal string PlayerName => _playerName;

        internal void NotifyPlayerJoined(string playerName) => OnPlayerJoined?.Invoke(playerName);
        internal void NotifyPlayerLeft(string playerName) => OnPlayerLeft?.Invoke(playerName);
        internal void NotifyPlayerReadyChanged() => OnPlayerReadyChanged?.Invoke();
        internal void NotifyAllPlayersReady() => OnAllPlayersReady?.Invoke();
        internal void NotifyMasterSwitched() => OnMasterSwitched?.Invoke();

        internal void SetMasterRole(bool isMaster)
        {
            if (_isMaster == isMaster) return;
            _isMaster = isMaster;
            OnMasterSwitched?.Invoke();
        }
    }
}
