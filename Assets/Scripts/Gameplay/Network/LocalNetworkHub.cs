using System.Collections.Generic;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// 本地联机模拟的消息路由中心。
    /// 所有 LocalNetworkService 实例共享，消息直接方法调用，零网络延迟。
    /// </summary>
    public static class LocalNetworkHub
    {
        private static readonly List<LocalNetworkService> _players = new();
        private static readonly Dictionary<int, bool> _readyPlayers = new();
        private static int _nextActorNumber = 1;
        private static bool _allPlayersReady;

        public static int Register(LocalNetworkService player)
        {
            int actorNumber = _nextActorNumber++;
            _players.Add(player);
            _readyPlayers[actorNumber] = false;
            _allPlayersReady = false;
            return actorNumber;
        }

        public static void Unregister(LocalNetworkService player)
        {
            if (!_players.Remove(player)) return;

            _readyPlayers.Remove(player.LocalActorNumber);
            foreach (var remaining in _players)
                remaining.NotifyPlayerLeft(player.LocalPlayerName);

            for (int i = 0; i < _players.Count; i++)
                _players[i].SetMasterRole(i == 0);

            _allPlayersReady = false;
        }

        public static void Clear()
        {
            _players.Clear();
            _readyPlayers.Clear();
            _nextActorNumber = 1;
            _allPlayersReady = false;
        }

        public static void NotifyPlayerJoined(LocalNetworkService joiningPlayer)
        {
            foreach (var player in _players)
            {
                if (player == joiningPlayer) continue;
                player.NotifyPlayerJoined(joiningPlayer.PlayerName);
                joiningPlayer.NotifyPlayerJoined(player.PlayerName);
            }
        }

        public static void SetPlayerReady(LocalNetworkService player, bool ready)
        {
            if (!_readyPlayers.TryGetValue(player.LocalActorNumber, out bool previous) || previous == ready)
                return;

            _readyPlayers[player.LocalActorNumber] = ready;
            foreach (var current in _players)
                current.NotifyPlayerReadyChanged();

            bool allReady = AreAllPlayersReady;
            if (allReady && !_allPlayersReady)
            {
                foreach (var current in _players)
                    current.NotifyAllPlayersReady();
            }
            _allPlayersReady = allReady;
        }

        public static bool AreAllPlayersReady
        {
            get
            {
                if (_players.Count == 0) return false;
                foreach (var player in _players)
                {
                    if (!_readyPlayers.TryGetValue(player.LocalActorNumber, out bool ready) || !ready)
                        return false;
                }
                return true;
            }
        }

        public static int PlayerCount => _players.Count;

        public static void SendToAll(string key, object value, int senderActor)
        {
            // 不把消息发回给发送者（防止 Master 收到自己广播的消息后再次广播导致无限递归）
            for (int i = 0; i < _players.Count; i++)
                if (_players[i] != null && _players[i].LocalActorNumber != senderActor)
                    _players[i].ReceiveEvent(key, value, senderActor);
        }

        public static void SendToMaster(string key, object value, int senderActor)
        {
            if (_players.Count > 0)
                _players[0]?.ReceiveEvent(key, value, senderActor);
        }

        public static void SendToPlayer(int targetActor, string key, object value, int senderActor)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i] != null && _players[i].LocalActorNumber == targetActor)
                {
                    _players[i].ReceiveEvent(key, value, senderActor);
                    return;
                }
            }
        }

        public static int[] GetAllActorNumbers()
        {
            var nums = new int[_players.Count];
            for (int i = 0; i < _players.Count; i++)
                nums[i] = _players[i]?.LocalActorNumber ?? 0;
            return nums;
        }

        public static string[] GetAllPlayerNames()
        {
            var names = new string[_players.Count];
            for (int i = 0; i < _players.Count; i++)
                names[i] = _players[i]?.LocalPlayerName ?? $"Player{i}";
            return names;
        }
    }
}
