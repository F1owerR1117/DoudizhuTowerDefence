using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>攻击节拍里一格可以附带的效果类型</summary>
    public enum RhythmEffectType
    {
        None = 0,
        Bleed = 1,
        Ignite = 2,
        Impair = 3,
        Stun = 4,
        Heavy = 5,
        Execute = 6,
        /// <summary>击杀召唤：本段伤害若击杀目标，按其 value（数量）触发召唤</summary>
        SummonOnKill = 7,
    }

    /// <summary>一格里的一条效果</summary>
    [System.Serializable]
    public class RhythmEffect
    {
        [Tooltip("效果类型")]
        public RhythmEffectType type = RhythmEffectType.Bleed;

        [Tooltip("数值含义按类型：\n" +
                 "Bleed / Ignite = 每秒伤害占攻击力的比例\n" +
                 "Impair = 减速比例（0~1）\n" +
                 "Stun = 眩晕秒数\n" +
                 "Heavy = 击退距离（世界单位）\n" +
                 "Execute = 处决 HP 阈值（绝对值，与现有 executeThreshold 同口径）\n" +
                 "SummonOnKill = 击杀时召唤的数量")]
        public float value = 0.05f;

        [Tooltip("持续时间（Bleed / Ignite / Impair / Stun 使用）")]
        public float duration = 3f;

        [Tooltip("最大叠层（Bleed / Ignite 使用）")]
        public int maxStacks = 3;

        [Tooltip("点燃扩散半径（仅 Ignite 使用；0 = 不扩散）")]
        public float spreadRadius;

        [Tooltip("点燃扩散所需的停留时间（仅 Ignite 使用）")]
        public float spreadDwell;
    }

    /// <summary>一段命中对应的一格：伤害倍率 + 该格附带的全部效果</summary>
    [System.Serializable]
    public class RhythmStep
    {
        [Tooltip("这一段的伤害倍率（<=0 时按 1 处理）")]
        public float multiplier = 1f;

        [Tooltip("这一段附带的效果，可留空 = 纯伤害；可同时挂多条")]
        public RhythmEffect[] effects;
    }

    /// <summary>
    /// 攻击节拍：每段命中（OnAttackHitFrame）对应一格。
    ///
    /// 严格 1:1 —— steps.Length 必须等于单位的 HitCount，否则视为配置错误，
    /// 运行时回退为恒定伤害并打印一次错误（不做循环补齐，避免掩盖配置问题）。
    /// 每次挥击开始时回到第 0 格；一次挥击内按 0→1→2 顺序推进，不循环。
    /// </summary>
    [System.Serializable]
    public class AttackRhythm
    {
        [Tooltip("启用攻击节拍（格数必须等于 HitCount）")]
        public bool enable;

        [Tooltip("每一段命中对应一格；格数必须 == HitCount")]
        public RhythmStep[] steps = new RhythmStep[0];

        /// <summary>当前段号；-1 表示本次挥击还没打出第一段</summary>
        [System.NonSerialized] private int _index = -1;

        public bool IsEnabled
        {
            get { return enable && steps != null && steps.Length > 0; }
        }

        public int Count
        {
            get { return steps != null ? steps.Length : 0; }
        }

        /// <summary>当前格号（越界时收敛到最后一格）</summary>
        public int CurrentIndex
        {
            get
            {
                if (Count <= 0) return 0;
                if (_index < 0) return 0;
                return _index >= Count ? Count - 1 : _index;
            }
        }

        /// <summary>当前这一格；未启用或没有配置时返回 null</summary>
        public RhythmStep Current
        {
            get
            {
                if (!IsEnabled) return null;
                return steps[CurrentIndex];
            }
        }

        /// <summary>当前这一段的伤害倍率（未启用恒为 1）</summary>
        public float Multiplier
        {
            get
            {
                var s = Current;
                if (s == null) return 1f;
                return s.multiplier > 0f ? s.multiplier : 1f;
            }
        }

        /// <summary>当前这一段允许的处决阈值；0 表示这一段不处决</summary>
        public float ExecuteThreshold
        {
            get
            {
                var s = Current;
                if (s == null || s.effects == null) return 0f;
                for (int i = 0; i < s.effects.Length; i++)
                {
                    var e = s.effects[i];
                    if (e != null && e.type == RhythmEffectType.Execute)
                        return e.value > 0f ? e.value : 0f;
                }
                return 0f;
            }
        }

        /// <summary>当前这一段的击杀召唤数量；0 表示这一段击杀不召唤</summary>
        public int SummonOnKillCount
        {
            get
            {
                var s = Current;
                if (s == null || s.effects == null) return 0;
                int total = 0;
                for (int i = 0; i < s.effects.Length; i++)
                {
                    var e = s.effects[i];
                    if (e != null && e.type == RhythmEffectType.SummonOnKill)
                        total += Mathf.Max(0, Mathf.RoundToInt(e.value));
                }
                return total;
            }
        }

        /// <summary>回到第一格（每次挥击开始时调用）</summary>
        public void Restart()
        {
            _index = -1;
        }

        /// <summary>推进到下一段（每段命中开始时调用）</summary>
        public void Advance()
        {
            if (!IsEnabled) { _index = -1; return; }
            _index++;
            if (_index > Count - 1) _index = Count - 1;
        }
    }
}
