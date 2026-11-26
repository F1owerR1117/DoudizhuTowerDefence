using System.Collections.Generic;
using DoudizhuTower.Gameplay.Entities;

namespace DoudizhuTower.Gameplay.Battle
{
    /// <summary>
    /// 伤害批量结算队列。
    /// 同帧内所有攻击意图先入队，帧末统一结算 HP 扣除和死亡判定，
    /// 确保执行顺序不影响战斗结果（方案 C）。
    /// </summary>
    public static class DamageQueue
    {
        private struct DamageEntry
        {
            public CardUnit Target;
            public float FinalDamage;
            /// <summary>伤害来源（用于处决等按来源判定的被动）；可为空</summary>
            public CardUnit Source;
            /// <summary>本次伤害所属「段」的处决阈值；0 = 这一段不处决</summary>
            public float ExecuteThreshold;
            /// <summary>本次伤害所属「段」的击杀召唤数量；0 = 这一段击杀不召唤</summary>
            public int SummonOnKill;
        }

        private static readonly List<DamageEntry> _queue = new();

        /// <summary>当前帧是否有待结算的伤害</summary>
        public static bool HasPending => _queue.Count > 0;

        /// <summary>清空队列（场景切换时调用，防止残留 CardUnit 引用）</summary>
        public static void Clear() => _queue.Clear();

        /// <summary>
        /// 入队一条伤害（由 CardUnit.TakeDamage 在批量模式下调用）。
        /// 调用前必须完成所有减伤计算，finalDamage 为最终扣血值。
        /// </summary>
        public static void Enqueue(CardUnit target, float finalDamage)
        {
            Enqueue(target, finalDamage, null);
        }

        /// <summary>
        /// 入队一条伤害，并记录伤害来源（供处决等按来源判定的被动使用）。
        /// </summary>
        public static void Enqueue(CardUnit target, float finalDamage, CardUnit source)
        {
            Enqueue(target, finalDamage, source, 0f, 0);
        }

        /// <summary>
        /// 入队一条伤害，附带「本次伤害所属段的处决阈值」。
        /// 该阈值必须随条目一起结算：批量模式下伤害延迟到帧末，
        /// 届时攻击者的节拍可能已经推进到下一段。
        /// </summary>
        public static void Enqueue(CardUnit target, float finalDamage, CardUnit source, float executeThreshold)
        {
            Enqueue(target, finalDamage, source, executeThreshold, 0);
        }

        /// <summary>
        /// 入队一条伤害，附带「处决阈值 + 击杀召唤数量」。
        /// </summary>
        public static void Enqueue(CardUnit target, float finalDamage, CardUnit source, float executeThreshold, int summonOnKill)
        {
            if (target == null || !target.IsAlive) return;
            _queue.Add(new DamageEntry
            {
                Target = target,
                FinalDamage = finalDamage,
                Source = source,
                ExecuteThreshold = executeThreshold,
                SummonOnKill = summonOnKill
            });
        }

        /// <summary>
        /// 结算队列中的所有伤害。
        /// 支持级联：死亡爆炸等触发的新伤害也会在同帧内结算。
        /// </summary>
        public static void ProcessAll()
        {
            if (_queue.Count == 0) return;

            // 级联循环：处理过程中可能产生新伤害（死亡爆炸、伤害共享等）
            int safety = 0;
            while (_queue.Count > 0 && safety < 10)
            {
                safety++;

                // 快照当前队列，处理期间新入队的伤害下轮处理
                var batch = new DamageEntry[_queue.Count];
                _queue.CopyTo(batch);
                _queue.Clear();

                foreach (var entry in batch)
                {
                    if (entry.Target == null || !entry.Target.IsAlive) continue;
                    entry.Target.ApplyDamage(entry.FinalDamage, entry.Source, entry.ExecuteThreshold, entry.SummonOnKill);
                }
            }

            if (safety >= 10)
                _queue.Clear();
        }
    }
}
