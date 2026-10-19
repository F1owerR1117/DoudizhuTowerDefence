using Fusion;
using System;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    // =========================
    // ① 全局游戏状态
    // =========================
    public struct GameState : INetworkStruct
    {
        public int Seed;
        public byte Phase;           // 0=叫分 1=出牌 2=结算
        public byte TurnSlot;        // 当前行动玩家
        public byte DeckCount;       // 剩余牌数

        // 叫分状态（Fusion Networked）
        public byte CurrentBidTurn;  // 当前叫分轮到谁
        public byte HighestBid;      // 最高叫分
        public byte HighestBidder;   // 最高叫分者 slot
        public byte BidCount;        // 已叫分人数
        public byte BidWinnerSlot;   // 叫分赢家 slot
        public byte IsBiddingFinished; // 0=进行中 1=已结束

        public int TickCounter;      // Heartbeat
        public int StateHash;        // 同步验证

        // 领域状态
        public byte DomainActive;
        public byte DomainType;
        public byte DomainSlot;
        public byte DomainMainRank;
        public byte DomainLength;
        public int DomainEndTick;
        public byte DomainPending;
        public int DomainCooldownEndTick;
        public byte CounterPending;
        public byte CounterActive;
        public byte CounterMainRank;
        public byte CounterType;
        public int CounterEndTick;
        public int CounterCooldownEndTick;

        // 身份初始化锁（0=未就绪 1=Host 已写入全部身份数据）
        public byte IdentityReady;
        // 0=叫分完成前未初始化牌局，1=Host 已发牌并进入对局。
        public byte MatchReady;
        public byte SceneReadyMask;
        public byte GameStarted;
        public byte GameOver;
        public byte WinnerIsLandlord;
        public int MatchStartTick;

        // 叫分阶段起始 Tick（网络同步，供 Client 计算倒计时）
        public int StateStartTick;

        // Host 分配的 PlayerRef → Slot 映射（存储 PlayerRef.RawEncoded 的低 8 位）
        public byte Slot0PlayerRef;
        public byte Slot1PlayerRef;
        public byte Slot2PlayerRef;
    }

    // =========================
    // ② 玩家状态（核心替代 slotHands + economy）
    // =========================
    public struct PlayerState : INetworkStruct
    {
        public byte Slot;         // 0/1/2
        public byte IsAI;         // 0=真人 1=AI
        public byte Role;         // 0=未定 1=地主 2=农民
        public byte Bid;          // 叫分值 (0=未叫 1/2/3)

        public float Gold;
        public float IncomeRate;
        public float GoldIncomeRemainder;
        public float AutomaticDrawElapsed;
        public float IncomeGrowthElapsed;
        public int TransferCooldownEndTick;

        public byte HandCount;

        public byte IsLandlord;   // 0=false, 1=true（避免 bool）
    }

    // =========================
    // ③ 战斗单位状态（唯一真相源）
    // =========================
    public struct UnitState : INetworkStruct
    {
        public int UnitId;
        public int Owner;         // 所属玩家 Slot

        public float PosX;        // 位置
        public float PosY;

        public int HP;
        public int MaxHP;

        public int ATK;           // 攻击力
        public float AttackSpeed; // 攻击间隔（秒）
        public float AttackTimer; // 攻击冷却计时
        public float AttackRange; // 攻击范围

        public int TargetId;      // 攻击目标 (-1=无目标)

        public byte State;        // 0=Idle 1=Move 2=Attack 3=Dead

        public float MoveSpeed;   // 移动速度
        public byte IsLandlord;   // 0=false, 1=true
    }

    // =========================
    // ④ 卡牌系统（完全压缩）
    // =========================
    public struct FusionCard : INetworkStruct
    {
        public byte Id;   // 0~52
    }

    // =========================
    // ⑤ 输入系统（传输层：指令容器，不绑定业务数据结构）
    // =========================
    public struct FusionPlayerInput : INetworkInput
    {
        public byte Action;       // 0=none, 1=play, 2=draw, 3=bid, 4=domain
        public byte Slot;         // 玩家槽位
        public byte DataLength;   // 有效数据长度（D0-Dn）
        public byte D0;           // 通用数据缓冲区
        public byte D1;
        public byte D2;
        public byte D3;
        public byte D4;
        public byte D5;
        public byte D6;
        public byte D7;

        /// <summary>写入任意字节数据</summary>
        public void SetData(byte[] data)
        {
            DataLength = (byte)(data?.Length ?? 0);
            if (DataLength > 0) D0 = data[0];
            if (DataLength > 1) D1 = data[1];
            if (DataLength > 2) D2 = data[2];
            if (DataLength > 3) D3 = data[3];
            if (DataLength > 4) D4 = data[4];
            if (DataLength > 5) D5 = data[5];
            if (DataLength > 6) D6 = data[6];
            if (DataLength > 7) D7 = data[7];
        }

        /// <summary>读取任意字节数据</summary>
        public byte[] GetData()
        {
            if (DataLength == 0) return System.Array.Empty<byte>();
            var result = new byte[DataLength];
            if (DataLength > 0) result[0] = D0;
            if (DataLength > 1) result[1] = D1;
            if (DataLength > 2) result[2] = D2;
            if (DataLength > 3) result[3] = D3;
            if (DataLength > 4) result[4] = D4;
            if (DataLength > 5) result[5] = D5;
            if (DataLength > 6) result[6] = D6;
            if (DataLength > 7) result[7] = D7;
            return result;
        }
    }

    // =========================
    // ⑥ 世界状态容器
    // =========================

    public enum GamePhase : byte
    {
        Lobby = 0,
        Bidding = 1,
        Playing = 2,
        End = 3,
    }

    public struct WorldState : INetworkStruct
    {
        public GameState Game;
        public PlayerState Player0;
        public PlayerState Player1;
        public PlayerState Player2;
    }

    // =========================
    // ⑦ 单位状态常量
    // =========================
    public static class UnitStateConstants
    {
        public const byte Idle = 0;
        public const byte Move = 1;
        public const byte Attack = 2;
        public const byte Dead = 3;
    }
}
