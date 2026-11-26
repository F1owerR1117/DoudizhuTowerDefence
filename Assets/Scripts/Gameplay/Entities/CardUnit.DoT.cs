using System.Collections.Generic;
using DoudizhuTower.Core.Battle;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 持续伤害与减益载体的运行时状态（partial class）。
    /// 流血 / 燃烧 / 撕裂 / 损伤 都记录在**受击者**身上，由受击者自己结算。
    ///
    /// 流血 / 燃烧 / 撕裂 统一采用「按兵种来源独立叠层、效果相加」的模型：
    /// 同一兵种（UnitTypeKey 相同）的多次施加合并为同一份，
    /// 不同兵种各自一份，互不覆盖。
    /// </summary>
    public partial class CardUnit
    {
        /// <summary>一份持续状态记录（对应一个兵种来源）</summary>
        public class DotInstance
        {
            public string sourceKey;   // 兵种标识
            public int stacks;         // 该来源的层数
            public float perStack;     // 每层数值（流血/燃烧=每秒伤害，撕裂=每层易伤%）
            public float timer;        // 剩余时间
            public float duration;     // 基础时长（受伤刷新用）
            public int maxStacks;      // 该来源的层数上限
        }

        private readonly List<DotInstance> _bleeds = new List<DotInstance>();
        private readonly List<DotInstance> _ignites = new List<DotInstance>();
        private readonly List<DotInstance> _tears = new List<DotInstance>();

        private bool _bleedTicking;
        private float _bleedTickAcc;
        private float _igniteTickAcc;

        // ─── 燃烧扩散配置（取最近一次施加的参数）──
        public float IgniteSpreadRadius { get; set; }
        public float IgniteSpreadDwell { get; set; } = 1.5f;
        private float _igniteSpreadTimer;
        private float _igniteSpreadCheckAcc;
        private static readonly Collider2D[] _igniteBuffer = new Collider2D[32];
        private static readonly ContactFilter2D _igniteFilter = new ContactFilter2D { useTriggers = true, useLayerMask = false };

        // ─── 损伤（减速）──────────────────────────
        /// <summary>损伤减速剩余时间</summary>
        public float ImpairTimer { get; set; }
        /// <summary>损伤的移速乘数（1 = 无减速）</summary>
        public float ImpairMoveMult { get; set; } = 1f;

        private const string SlowBuffId = "slow_aura";

        // ─── 汇总（只读）──────────────────────────
        /// <summary>所有来源的流血总层数</summary>
        public int BleedStacks => SumStacks(_bleeds);
        /// <summary>所有来源的燃烧总层数</summary>
        public int IgniteStacks => SumStacks(_ignites);
        /// <summary>所有来源的撕裂总层数</summary>
        public int TearStacks => SumStacks(_tears);

        public bool IsBleeding => _bleeds.Count > 0;
        public bool IsBurning => _ignites.Count > 0;

        public int BleedSourceCount => _bleeds.Count;
        public int IgniteSourceCount => _ignites.Count;
        public int TearSourceCount => _tears.Count;

        /// <summary>撕裂易伤倍率：1 + Σ(层数 × 每层百分比)</summary>
        public float TearMultiplier
        {
            get
            {
                float bonus = 0f;
                for (int i = 0; i < _tears.Count; i++)
                    bonus += _tears[i].stacks * _tears[i].perStack;
                return 1f + bonus;
            }
        }

        private static int SumStacks(List<DotInstance> list)
        {
            int total = 0;
            for (int i = 0; i < list.Count; i++) total += list[i].stacks;
            return total;
        }

        /// <summary>
        /// 向某个列表施加一份状态：同兵种来源合并叠层并刷新时长，不同兵种各自一份。
        /// </summary>
        private static void ApplyTo(List<DotInstance> list, string sourceKey, float perStack, float duration, int maxStacks)
        {
            int max = Mathf.Max(1, maxStacks);
            if (sourceKey == null) sourceKey = string.Empty;

            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if (d.sourceKey != sourceKey) continue;

                d.stacks = Mathf.Min(d.stacks + 1, max);
                d.perStack = perStack;
                d.maxStacks = max;
                d.duration = Mathf.Max(d.duration, duration);
                d.timer = Mathf.Max(d.timer, duration);
                return;
            }

            list.Add(new DotInstance
            {
                sourceKey = sourceKey,
                stacks = 1,
                perStack = perStack,
                timer = duration,
                duration = duration,
                maxStacks = max
            });
        }

        /// <summary>递减计时并清理到期条目</summary>
        private static void TickList(List<DotInstance> list, float dt)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                list[i].timer -= dt;
                if (list[i].timer <= 0f) list.RemoveAt(i);
            }
        }

        /// <summary>每秒伤害总和（流血 / 燃烧共用）</summary>
        private static float SumDps(List<DotInstance> list)
        {
            float total = 0f;
            for (int i = 0; i < list.Count; i++) total += list[i].perStack * list[i].stacks;
            return total;
        }

        // ══════════════════════════════════════════
        //  对外施加接口
        // ══════════════════════════════════════════

        /// <summary>施加流血（按兵种来源独立叠层）</summary>
        public void ApplyBleed(CardUnit source, float dpsPerStack, float duration, int maxStacks)
        {
            if (!SimulatesCombat) return;
            ApplyTo(_bleeds, source != null ? source.UnitTypeKey : null, dpsPerStack, duration, maxStacks);
        }

        /// <summary>施加燃烧（按兵种来源独立叠层；携带扩散参数）</summary>
        public void ApplyIgnite(CardUnit source, float dpsPerStack, float duration, int maxStacks,
                                float spreadRadius = 0f, float spreadDwell = 1.5f)
        {
            if (!SimulatesCombat) return;
            ApplyTo(_ignites, source != null ? source.UnitTypeKey : null, dpsPerStack, duration, maxStacks);
            IgniteSpreadRadius = spreadRadius;
            IgniteSpreadDwell = spreadDwell;
        }

        /// <summary>施加撕裂（按兵种来源独立叠层，每层增加受到的伤害）</summary>
        public void ApplyTear(CardUnit source, float perStack, float duration, int maxStacks)
        {
            if (!SimulatesCombat) return;
            ApplyTo(_tears, source != null ? source.UnitTypeKey : null, perStack, duration, maxStacks);
        }

        /// <summary>施加损伤减速（与减速光环取更强）</summary>
        public void ApplyImpair(float slowPercent, float duration)
        {
            if (!SimulatesCombat) return;
            float mult = 1f - Mathf.Clamp01(slowPercent);
            ImpairMoveMult = Mathf.Min(ImpairMoveMult, mult);   // 更强者优先
            ImpairTimer = Mathf.Max(ImpairTimer, duration);
            RefreshSlowBuff();
        }

        /// <summary>
        /// 受到真实伤害后调用：刷新**所有**来源的流血持续时间。
        /// 流血自身的每秒伤害不会触发这里（否则会永久自续）。
        /// </summary>
        public void RefreshBleedOnDamaged()
        {
            if (_bleeds.Count == 0) return;
            if (_bleedTicking) return;

            for (int i = 0; i < _bleeds.Count; i++)
                _bleeds[i].timer = _bleeds[i].duration;
        }

        // ══════════════════════════════════════════
        //  每帧结算
        // ══════════════════════════════════════════

        /// <summary>每帧结算流血 / 燃烧 / 撕裂 / 损伤（仅 Master 端）</summary>
        private void UpdateDoT()
        {
            if (!SimulatesCombat) return;
            float dt = Time.deltaTime;

            // ── 流血 ──
            if (_bleeds.Count > 0)
            {
                TickList(_bleeds, dt);
                _bleedTickAcc += dt;
                while (_bleedTickAcc >= 1f && _bleeds.Count > 0)
                {
                    _bleedTickAcc -= 1f;
                    float total = SumDps(_bleeds);
                    if (total > 0f)
                    {
                        _bleedTicking = true;
                        try { TakeDamage(total, DamageType.Burn); }
                        finally { _bleedTicking = false; }
                    }
                    if (!IsAlive) return;
                }
            }
            else _bleedTickAcc = 0f;

            // ── 燃烧 ──
            if (_ignites.Count > 0)
            {
                TickList(_ignites, dt);
                _igniteTickAcc += dt;
                while (_igniteTickAcc >= 1f && _ignites.Count > 0)
                {
                    _igniteTickAcc -= 1f;
                    float total = SumDps(_ignites);
                    if (total > 0f) TakeDamage(total, DamageType.Burn);
                    if (!IsAlive) return;
                }
            }
            else _igniteTickAcc = 0f;

            // ── 撕裂：只维护计时，不造成伤害 ──
            if (_tears.Count > 0) TickList(_tears, dt);

            // ── 损伤：到期恢复 ──
            if (ImpairTimer > 0f)
            {
                ImpairTimer -= dt;
                if (ImpairTimer <= 0f)
                {
                    ImpairTimer = 0f;
                    ImpairMoveMult = 1f;
                    RefreshSlowBuff();
                }
            }

            // ── 燃烧扩散 ──
            if (_ignites.Count > 0 && IgniteSpreadRadius > 0f)
                UpdateIgniteSpread(dt);
        }

        /// <summary>减速统一通道：光环与损伤两路各给出一个乘数，取更强（更小）的那个生效。</summary>
        public void RefreshSlowBuff()
        {
            if (!SimulatesCombat) return;
            float mult = Mathf.Min(SlowAuraMoveMult, ImpairMoveMult);
            if (mult < 0.9999f)
                ApplyBuff(SlowBuffId, new StatBuff(moveSpeed: mult));
            else
                RemoveBuff(SlowBuffId);
        }

        /// <summary>
        /// 燃烧扩散：同阵营单位在范围内累计停留足够久则被点燃。
        /// 扩散沿用「当前每层伤害最高」的那份来源，保持来源标识一致。
        /// </summary>
        private void UpdateIgniteSpread(float dt)
        {
            _igniteSpreadCheckAcc += dt;
            float step = _igniteSpreadCheckAcc;
            if (step < 0.25f) return;
            _igniteSpreadCheckAcc = 0f;

            int num = Physics2D.OverlapCircle(VisualCenter, IgniteSpreadRadius, _igniteFilter, _igniteBuffer);
            bool anyInRange = false;
            for (int i = 0; i < num; i++)
            {
                var other = _igniteBuffer[i].GetComponentInParent<CardUnit>();
                if (other == null || other == this || !other.IsAlive || other._isBuilding) continue;
                if (other.IsLandlord != IsLandlord) continue;
                anyInRange = true;
                break;
            }

            if (!anyInRange) { _igniteSpreadTimer = 0f; return; }

            _igniteSpreadTimer += step;
            if (_igniteSpreadTimer < Mathf.Max(0.1f, IgniteSpreadDwell)) return;
            _igniteSpreadTimer = 0f;

            // 取最强的一份作为扩散源（保留其来源标识）
            DotInstance src = _ignites[0];
            for (int i = 1; i < _ignites.Count; i++)
                if (_ignites[i].perStack > src.perStack) src = _ignites[i];

            for (int i = 0; i < num; i++)
            {
                var other = _igniteBuffer[i].GetComponentInParent<CardUnit>();
                if (other == null || other == this || !other.IsAlive || other._isBuilding) continue;
                if (other.IsLandlord != IsLandlord) continue;

                // 直接用来源标识与参数施加（不再依赖对方是否有燃烧被动）
                ApplyTo(other._ignites, src.sourceKey, src.perStack, src.duration, src.maxStacks);
                other.IgniteSpreadRadius = IgniteSpreadRadius;
                other.IgniteSpreadDwell = IgniteSpreadDwell;
            }
        }

        /// <summary>清理所有持续状态（回池 / 死亡时调用）</summary>
        public void ClearDoT()
        {
            _bleeds.Clear(); _bleedTickAcc = 0f; _bleedTicking = false;
            _ignites.Clear(); _igniteTickAcc = 0f;
            _tears.Clear();
            IgniteSpreadRadius = 0f; _igniteSpreadTimer = 0f; _igniteSpreadCheckAcc = 0f;
            ImpairTimer = 0f; ImpairMoveMult = 1f;
        }
    }
}
