using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>节拍启用后，原有全局命中被动（enableBleed 等）如何处理</summary>
    public enum LegacyOnHitMode
    {
        /// <summary>接管：只用节拍格子里的效果，忽略全局勾选（推荐）</summary>
        Ignore = 0,
        /// <summary>叠加：全局被动仍每段生效，再叠加格子里的效果</summary>
        Add = 1,
    }

    /// <summary>
    /// 攻击节拍（Rhythm）的运行时装配：
    /// - 每段命中取一格，格内自带倍率与效果清单
    /// - 严格 1:1：格数必须等于 HitCount，否则报错并回退为恒定伤害
    /// - 全局命中被动的去留由 <see cref="legacyOnHitPassives"/> 决定
    /// </summary>
    public partial class UnitPassives
    {
        [Header("── 攻击节拍（每段命中不同伤害 / 效果）──")]
        [Tooltip("格数必须等于单位的 HitCount，严格一一对应")]
        public AttackRhythm rhythm = new AttackRhythm();

        [Tooltip("节拍启用后，原有全局命中被动怎么处理：接管（推荐）/ 叠加")]
        public LegacyOnHitMode legacyOnHitPassives = LegacyOnHitMode.Ignore;

        [System.NonSerialized] private bool _rhythmChecked;
        [System.NonSerialized] private bool _rhythmActive;

        /// <summary>节拍是否生效（已启用且格数与 HitCount 匹配）</summary>
        public bool RhythmActive
        {
            get
            {
                ValidateRhythm();
                return _rhythmActive;
            }
        }

        /// <summary>当前这一段的伤害倍率；节拍未生效时恒为 1</summary>
        public float CurrentRhythmMultiplier
        {
            get { return RhythmActive ? rhythm.Multiplier : 1f; }
        }

        /// <summary>
        /// 当前这一段允许的处决阈值（0 = 不处决）。
        /// 节拍生效时取这一格里的 Execute 效果；否则回落到原有全局 enableExecute + executeThreshold。
        /// </summary>
        public float CurrentExecuteThreshold
        {
            get
            {
                if (RhythmActive) return rhythm.ExecuteThreshold;
                return enableExecute ? executeThreshold : 0f;
            }
        }

        /// <summary>
        /// 当前这一段的击杀召唤数量（0 = 不召唤）。
        /// 节拍生效时取这一格里的 SummonOnKill 效果；否则为 0（由原有全局击杀召唤接管）。
        /// </summary>
        public int CurrentSummonOnKill
        {
            get { return RhythmActive ? rhythm.SummonOnKillCount : 0; }
        }

        /// <summary>每次挥击开始时调用：回到第一格</summary>
        public void RestartRhythm()
        {
            if (rhythm != null) rhythm.Restart();
            ValidateRhythm();
        }

        /// <summary>每段命中开始时调用：进入下一格</summary>
        public void AdvanceRhythm()
        {
            if (rhythm != null) rhythm.Advance();
        }

        /// <summary>校验格数与 HitCount 是否严格一致；不一致则报错并停用节拍</summary>
        private void ValidateRhythm()
        {
            if (_rhythmChecked) return;
            if (rhythm == null) { _rhythmChecked = true; _rhythmActive = false; return; }
            if (!rhythm.enable) { _rhythmChecked = true; _rhythmActive = false; return; }
            if (_owner == null) return;   // Awake 尚未完成，下次再校验

            _rhythmChecked = true;
            int hitCount = Mathf.Max(1, _owner.Stats.HitCount);
            int stepCount = rhythm.Count;

            if (stepCount != hitCount)
            {
                _rhythmActive = false;
                Debug.LogError($"[Rhythm] {_owner.name} 节拍配置错误：steps={stepCount}，HitCount={hitCount}，两者必须严格相等；本次已回退为恒定伤害。");
                return;
            }

            _rhythmActive = true;
        }

        /// <summary>执行当前这一格附带的效果（Execute 不在这里处理，它走伤害结算）</summary>
        public void ApplyRhythmEffects(CardUnit target)
        {
            if (!RhythmActive || target == null || !target.IsAlive) return;

            var step = rhythm.Current;
            if (step == null || step.effects == null || step.effects.Length == 0) return;

            for (int i = 0; i < step.effects.Length; i++)
            {
                var e = step.effects[i];
                if (e == null || e.type == RhythmEffectType.None) continue;

                switch (e.type)
                {
                    case RhythmEffectType.Bleed:
                        target.ApplyBleed(_owner, _owner.Stats.ATK * e.value, e.duration, Mathf.Max(1, e.maxStacks));
                        _unitVFX?.PlayBleed(target.transform);
                        _unitAudio?.PlayBleed();
                        break;

                    case RhythmEffectType.Ignite:
                        target.ApplyIgnite(_owner, _owner.Stats.ATK * e.value, e.duration, Mathf.Max(1, e.maxStacks),
                                           e.spreadRadius, e.spreadDwell);
                        _unitVFX?.PlayIgnite(target.transform);
                        _unitAudio?.PlayIgnite();
                        break;

                    case RhythmEffectType.Impair:
                        target.ApplyImpair(e.value, e.duration);
                        _unitVFX?.PlayImpair(target.transform);
                        _unitAudio?.PlayImpair();
                        break;

                    case RhythmEffectType.Stun:
                        target.StunTimer = Mathf.Max(target.StunTimer, e.duration);
                        _owner.TriggerAnim("StunHit");
                        _unitAudio?.PlayStunHit();
                        _unitVFX?.PlayStunHit(target.transform);
                        break;

                    case RhythmEffectType.Heavy:
                        if (e.value > 0f)
                        {
                            Vector2 dir = (Vector2)target.VisualCenter - (Vector2)_owner.VisualCenter;
                            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                            target.InterruptAttack();
                            _unitVFX?.PlayHeavyStrike(target.VisualCenter, e.value);
                            _unitAudio?.PlayHeavyStrike();
                            StartCoroutine(KnockbackCoroutine(target, dir.normalized, e.value));
                        }
                        break;

                    case RhythmEffectType.Execute:
                        // 处决在伤害结算时按「本段阈值」判定，这里不做事
                        break;

                    case RhythmEffectType.SummonOnKill:
                        // 击杀召唤在目标真正死亡时触发（见 CardUnit 的击杀结算）
                        break;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 改数值后重新校验，避免编辑器里沿用旧的结论
            _rhythmChecked = false;
        }
#endif
    }
}
