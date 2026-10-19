using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// Desync 检测系统。
    /// 每 N 帧计算 WorldState + UnitBuffer 的 Hash，用于对比 Host/Client 是否一致。
    /// </summary>
    public class DesyncDetector
    {
        private const int HASH_INTERVAL = 10; // 每 10 帧计算一次 Hash

        private int _tickCounter;
        private uint _lastHash;

        /// <summary>
        /// 是否需要计算 Hash
        /// </summary>
        public bool ShouldComputeHash(int tick)
        {
            return tick % HASH_INTERVAL == 0;
        }

        /// <summary>
        /// 计算 WorldState 的 Hash
        /// </summary>
        public uint ComputeWorldHash(WorldState world, UnitBuffer units)
        {
            uint hash = 17;

            // WorldState Hash
            hash = hash * 31 + (uint)world.Game.Seed;
            hash = hash * 31 + world.Game.Phase;
            hash = hash * 31 + world.Game.TurnSlot;
            hash = hash * 31 + world.Game.DeckCount;
            hash = hash * 31 + world.Game.DomainActive;
            hash = hash * 31 + world.Game.DomainType;
            hash = hash * 31 + world.Game.DomainSlot;

            // PlayerState Hash
            hash = HashPlayerState(hash, world.Player0);
            hash = HashPlayerState(hash, world.Player1);
            hash = HashPlayerState(hash, world.Player2);

            // UnitBuffer Hash
            hash = hash * 31 + (uint)units.Count;
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units.Get(i);
                hash = HashUnitState(hash, unit);
            }

            _lastHash = hash;
            return hash;
        }

        private uint HashPlayerState(uint hash, PlayerState player)
        {
            hash = hash * 31 + player.Slot;
            hash = hash * 31 + (uint)System.BitConverter.SingleToInt32Bits(player.Gold);
            hash = hash * 31 + (uint)System.BitConverter.SingleToInt32Bits(player.IncomeRate);
            hash = hash * 31 + (uint)System.BitConverter.SingleToInt32Bits(player.GoldIncomeRemainder);
            hash = hash * 31 + (uint)System.BitConverter.SingleToInt32Bits(player.AutomaticDrawElapsed);
            hash = hash * 31 + (uint)System.BitConverter.SingleToInt32Bits(player.IncomeGrowthElapsed);
            hash = unchecked(hash * 31 + (uint)player.TransferCooldownEndTick);
            hash = hash * 31 + player.HandCount;
            hash = hash * 31 + player.IsLandlord;
            return hash;
        }

        private uint HashUnitState(uint hash, UnitState unit)
        {
            hash = hash * 31 + (uint)unit.UnitId;
            hash = hash * 31 + (uint)unit.Owner;
            hash = hash * 31 + FloatToUint(unit.PosX);
            hash = hash * 31 + FloatToUint(unit.PosY);
            hash = hash * 31 + (uint)unit.HP;
            hash = hash * 31 + (uint)unit.MaxHP;
            hash = hash * 31 + (uint)unit.ATK;
            hash = hash * 31 + FloatToUint(unit.AttackSpeed);
            hash = hash * 31 + FloatToUint(unit.AttackTimer);
            hash = hash * 31 + FloatToUint(unit.AttackRange);
            hash = hash * 31 + (uint)unit.TargetId;
            hash = hash * 31 + unit.State;
            hash = hash * 31 + FloatToUint(unit.MoveSpeed);
            hash = hash * 31 + unit.IsLandlord;
            return hash;
        }

        private uint FloatToUint(float f)
        {
            // 将 float 转换为 uint 用于 Hash（避免浮点精度问题）
            int bits = System.BitConverter.ToInt32(System.BitConverter.GetBytes(f), 0);
            return (uint)bits;
        }

        /// <summary>
        /// 获取上一次计算的 Hash
        /// </summary>
        public uint GetLastHash()
        {
            return _lastHash;
        }

        /// <summary>
        /// 对比两个 Hash 是否一致
        /// </summary>
        public static bool CompareHashes(uint hashA, uint hashB)
        {
            return hashA == hashB;
        }
    }
}
