using System.Collections.Generic;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// Animator 驱动的动画控制器。
    /// 通过 AnimatorOverrideController 替换基础控制器中的动画片段。
    /// 使用 Tools → 创建兵种 Animator Controller 生成基础控制器。
    /// </summary>
    public class SimpleAnimator : MonoBehaviour
    {
        [Header("-- 基础动画 --")]
        public AnimationClip idleClip;
        public AnimationClip walkClip;
        public AnimationClip attackClip;

        [Header("-- 死亡动画 --")]
        [Tooltip("死亡")]
        public AnimationClip deathClip;

        [Header("-- Trigger 特效动画 --")]
        [Tooltip("冲锋")]
        public AnimationClip chargeClip;
        [Tooltip("震波")]
        public AnimationClip shockwaveClip;
        [Tooltip("溅射")]
        public AnimationClip splashClip;
        [Tooltip("眩晕命中")]
        public AnimationClip stunHitClip;
        [Tooltip("君王光环")]
        public AnimationClip kingAuraClip;
        [Tooltip("死亡爆炸")]
        public AnimationClip deathExplosionClip;
        [Tooltip("燃烧")]
        public AnimationClip burnClip;
        [Tooltip("召唤")]
        public AnimationClip summonClip;
        [Tooltip("飞扑")]
        public AnimationClip pounceClip;

        [Header("-- BOSS 技能动画 --")]
        [Tooltip("冲刺")]
        public AnimationClip dashClip;
        [Tooltip("BOSS 技能 1")]
        public AnimationClip bossSkill1Clip;
        [Tooltip("BOSS 技能 2")]
        public AnimationClip bossSkill2Clip;
        [Tooltip("BOSS 技能 3")]
        public AnimationClip bossSkill3Clip;

        [Header("-- Bool 特效动画 --")]
        [Tooltip("嘲讽")]
        public AnimationClip tauntClip;
        [Tooltip("盾墙")]
        public AnimationClip shieldWallClip;

        [Header("-- 控制器 --")]
        [Tooltip("基础控制器（由 Tools 菜单自动生成）")]
        public RuntimeAnimatorController baseController;

        private Animator _animator;

        /// <summary>获取 Animator 组件引用</summary>
        public Animator Animator => _animator;

        // 动画名称到 Clip 的映射表
        private Dictionary<string, AnimationClip> _clipMap;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>(true);
            if (_animator == null)
                _animator = gameObject.AddComponent<Animator>();

            // 只有 Inspector 未赋值时，从默认路径加载
            if (baseController == null)
                baseController = UnityEngine.Resources.Load<RuntimeAnimatorController>("UnitBaseController");
#if UNITY_EDITOR
            if (baseController == null)
                baseController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/UnitBaseController.controller");
#endif

            if (baseController == null)
            {
                Debug.LogError($"[SimpleAnimator] {gameObject.name}: baseController 为 null！请在预制体 SimpleAnimator 组件上拖入 UnitBaseController");
                return;
            }

            // 初始化映射表
            _clipMap = new Dictionary<string, AnimationClip>
            {
                { "idle", idleClip },
                { "walk", walkClip },
                { "attack", attackClip },
                { "death", deathClip },
                { "charge", chargeClip },
                { "shockwave", shockwaveClip },
                { "splash", splashClip },
                { "stunhit", stunHitClip },
                { "kingaura", kingAuraClip },
                { "deathexplosion", deathExplosionClip },
                { "burn", burnClip },
                { "summon", summonClip },
                { "pounce", pounceClip },
                { "dash", dashClip },
                { "bossskill1", bossSkill1Clip },
                { "bossskill2", bossSkill2Clip },
                { "bossskill3", bossSkill3Clip },
                { "taunt", tauntClip },
                { "shieldwall", shieldWallClip }
            };

            var overrideCtrl = new AnimatorOverrideController(baseController);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideCtrl.GetOverrides(overrides);

            for (int i = 0; i < overrides.Count; i++)
            {
                var orig = overrides[i].Key;
                if (orig == null) continue;

                // 大小写不敏感匹配
                string nameLower = orig.name.ToLowerInvariant();
                foreach (var kvp in _clipMap)
                {
                    if (nameLower.Contains(kvp.Key) && kvp.Value != null)
                    {
                        overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, kvp.Value);
                        break;
                    }
                }
            }

            overrideCtrl.ApplyOverrides(overrides);

            // 替换控制器前断电 Animator，防止重置状态机时越狱到 Attack
            _animator.enabled = false;
            _animator.runtimeAnimatorController = overrideCtrl;
            _animator.SetInteger("State", 0);
            _animator.enabled = true;
            _animator.Play("Idle", 0, 0f);
            _animator.Update(0f);
        }

        /// <summary>
        /// 触发一次性动画参数（冲锋、震波等）。
        /// 由 CardUnit.TriggerAnim 调用。
        /// 如果动画剪辑未配置，则跳过触发。
        /// </summary>
        public void Trigger(string name)
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return;

            // 检查是否有对应的动画剪辑配置
            string nameLower = name.ToLower();
            if (_clipMap != null && _clipMap.TryGetValue(nameLower, out var clip))
            {
                // 有配置才触发
                if (clip != null)
                    _animator.SetTrigger(name);
            }
            // 没有配置时不触发，避免动画异常
        }

        /// <summary>
        /// 设置持续动画参数（嘲讽、盾墙等）。
        /// 由 CardUnit.SetAnimBool 调用。
        /// 如果动画剪辑未配置，则跳过设置。
        /// </summary>
        public void SetBool(string name, bool value)
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return;

            // 检查是否有对应的动画剪辑配置
            string nameLower = name.ToLower();
            if (_clipMap != null && _clipMap.TryGetValue(nameLower, out var clip))
            {
                // 有配置才设置
                if (clip != null)
                    _animator.SetBool(name, value);
            }
            // 如果没有配置，不设置，避免动画异常
        }

        /// <summary>
        /// 获取指定动画的长度（秒）。
        /// 用于 BOSS 技能施法时间自动同步。
        /// </summary>
        public float GetClipLength(string name)
        {
            if (_clipMap == null) return 0f;

            string nameLower = name.ToLower();
            if (_clipMap.TryGetValue(nameLower, out var clip) && clip != null)
                return clip.length;

            return 0f;
        }

        /// <summary>
        /// 获取当前播放的动画状态名称。
        /// </summary>
        public string GetCurrentStateName()
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return "";

            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            // 简化处理：返回默认状态名称
            return "Idle";
        }
    }
}
