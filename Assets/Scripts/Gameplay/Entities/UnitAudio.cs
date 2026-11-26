using System.Collections.Generic;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Gameplay.Systems;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 兵种音效组件：管理兵种的所有音效播放。
    /// 通过监听 CardUnit 的事件来自动播放对应音效。
    /// 使用优先级通道播放，确保 UI 音效不被战斗音效挤掉。
    /// 按 Clip 分组计数，避免单一兵种霸占通道；屏幕外兵种不播放音效。
    /// </summary>
    public class UnitAudio : MonoBehaviour
    {
        #region 音效配置

        [Header("-- 攻击音效 --")]
        [Tooltip("近战攻击音效，按打击帧索引分配不同音效（索引 0=第1帧，1=第2帧...超出范围时使用最后一个）")]
        [SerializeField] private AudioClip[] attackMeleeClips;

        [Tooltip("远程攻击音效（留空则不播放）")]
        [SerializeField] private AudioClip attackRangedClip;

        [Header("-- 受伤/死亡音效 --")]
        [Tooltip("受击音效（留空则不播放）")]
        [SerializeField] private AudioClip hitClip;

        [Tooltip("死亡音效（留空则不播放）")]
        [SerializeField] private AudioClip deathClip;

        [Header("-- 技能音效 --")]
        [Tooltip("冲锋音效（留空则不播放）")]
        [SerializeField] private AudioClip chargeClip;

        [Tooltip("震波音效（留空则不播放）")]
        [SerializeField] private AudioClip shockwaveClip;

        [Tooltip("溅射音效（留空则不播放）")]
        [SerializeField] private AudioClip splashClip;

        [Tooltip("眩晕命中音效（留空则不播放）")]
        [SerializeField] private AudioClip stunHitClip;

        [Tooltip("君王光环音效（留空则不播放）")]
        [SerializeField] private AudioClip kingAuraClip;

        [Tooltip("死亡爆炸音效（留空则不播放）")]
        [SerializeField] private AudioClip deathExplosionClip;

        [Tooltip("燃烧音效（留空则不播放）")]
        [SerializeField] private AudioClip burnClip;

        [Tooltip("召唤音效（留空则不播放）")]
        [SerializeField] private AudioClip summonClip;

        [Header("-- 持续状态音效 --")]
        [Tooltip("嘲讽音效（留空则不播放）")]
        [SerializeField] private AudioClip tauntClip;

        [Tooltip("盾墙音效（留空则不播放）")]
        [SerializeField] private AudioClip shieldWallClip;

        [Header("-- 命中附加状态音效 --")]
        [Tooltip("流血音效（留空则不播放）")]
        [SerializeField] private AudioClip bleedClip;

        [Tooltip("燃烧（点燃）音效（留空则不播放）")]
        [SerializeField] private AudioClip igniteClip;

        [Tooltip("损伤（减速）音效（留空则不播放）")]
        [SerializeField] private AudioClip impairClip;

        [Tooltip("重击（击退）音效（留空则不播放）")]
        [SerializeField] private AudioClip heavyStrikeClip;

        [Tooltip("过热音效（留空则不播放）")]
        [SerializeField] private AudioClip overheatClip;

        [Tooltip("护盾吸收音效（留空则不播放）")]
        [SerializeField] private AudioClip shieldAbsorbClip;

        [Tooltip("减速光环音效（留空则不播放）")]
        [SerializeField] private AudioClip slowAuraClip;

        [Tooltip("领袖光环音效（留空则不播放）")]
        [SerializeField] private AudioClip leaderClip;

        [Tooltip("快速连击爆发音效（留空则不播放）")]
        [SerializeField] private AudioClip burstClip;

        [Tooltip("处决音效（留空则不播放）")]
        [SerializeField] private AudioClip executeClip;

        [Tooltip("飞扑音效（留空则不播放）")]
        [SerializeField] private AudioClip pounceClip;

        [Tooltip("撕裂音效（留空则不播放）")]
        [SerializeField] private AudioClip tearClip;

        [Tooltip("治疗音效（留空则不播放）")]
        [SerializeField] private AudioClip healClip;

        [Header("-- 音量设置 --")]
        [Tooltip("音量缩放（0-1）")]
        [Range(0f, 1f)]
        [SerializeField] private float volumeScale = 1f;

        [Tooltip("受击音量缩放（通常比攻击音效低，避免太吵）")]
        [Range(0f, 1f)]
        [SerializeField] private float hitVolumeScale = 0.5f;

        #endregion

        #region 并发限制（按 Clip 分组）

        [Header("-- 并发限制 --")]
        [Tooltip("同一种音效最大同时播放数（不同兵种的音效互不影响）")]
        [SerializeField] private int maxPerClipConcurrent = 3;

        /// <summary>每种 AudioClip 当前正在播放的数量</summary>
        private static readonly Dictionary<AudioClip, int> _clipCounts = new();

        /// <summary>清除所有 Clip 并发计数（场景切换时调用，防止跨局泄漏）</summary>
        public static void ClearClipCounts() => _clipCounts.Clear();

        #endregion

        #region 屏幕可见性

        [Header("-- 可见性 --")]
        [Tooltip("屏幕外是否禁用音效")]
        [SerializeField] private bool cullOffScreen = true;

        /// <summary>屏幕外边距（Viewport 坐标），略微扩大判定范围避免边缘闪烁</summary>
        private const float ViewportMargin = 0.05f;

        #endregion

        #region 私有字段

        private CardUnit _unit;
        private Camera _mainCam;
        private readonly System.Collections.Generic.List<AudioClip> _pendingClips = new();

        #endregion

        #region 公开访问器

        /// <summary>获取受击音效 Clip（供网络同步使用）</summary>
        public AudioClip GetHitClip() => hitClip;

        /// <summary>获取死亡音效 Clip（供网络同步使用）</summary>
        public AudioClip GetDeathClip() => deathClip;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _unit = GetComponentInParent<CardUnit>();
            _mainCam = Camera.main;
        }

        private void OnEnable()
        {
            if (_unit != null)
            {
                _unit.OnAttackEvent += OnAttack;
                _unit.OnTakeDamageEvent += OnTakeDamage;
                _unit.OnDeathEvent += OnDeath;
            }
        }

        private void OnDisable()
        {
            if (_unit != null)
            {
                _unit.OnAttackEvent -= OnAttack;
                _unit.OnTakeDamageEvent -= OnTakeDamage;
                _unit.OnDeathEvent -= OnDeath;
            }

            // 取消所有待递减的协程，立即归还配额
            StopAllCoroutines();
            foreach (var clip in _pendingClips)
            {
                if (_clipCounts.TryGetValue(clip, out int count))
                {
                    if (count <= 1) _clipCounts.Remove(clip);
                    else _clipCounts[clip] = count - 1;
                }
            }
            _pendingClips.Clear();
        }

        #endregion

        #region 事件处理

        private void OnAttack(CardUnit target)
        {
            if (!CanPlay()) return;

            if (_unit.IsRanged)
            {
                PlayWithLimit(attackRangedClip, SfxChannel.CombatLow);
            }
            else if (attackMeleeClips != null && attackMeleeClips.Length > 0)
            {
                int frame = _unit.CurrentHitFrame;
                int index = Mathf.Clamp(frame, 0, attackMeleeClips.Length - 1);
                PlayWithLimit(attackMeleeClips[index], SfxChannel.CombatLow);
            }
        }

        private void OnTakeDamage(float damage, DamageType type)
        {
            // 受击不检查可见性（被打就有反馈，无论是否在屏幕内）
            var audio = AudioManager.Instance;
            if (audio == null) return;

            if (hitClip != null)
                audio.PlayCombat(hitClip, hitVolumeScale * volumeScale);
        }

        private void OnDeath()
        {
            // 死亡不检查可见性，走高优先级
            var audio = AudioManager.Instance;
            if (audio == null) return;

            if (deathClip != null)
                audio.PlayCombatHigh(deathClip, volumeScale);
        }

        #endregion

        #region 网络事件播放（Client 端使用）

        /// <summary>Client 端播放攻击音效</summary>
        public void PlayAttackNetwork()
        {
            if (_unit != null && !_unit.SimulatesCombat)
            {
                // Client 端：检查可见性后播放
                if (!CanPlay()) return;
            }

            if (_unit != null && _unit.IsRanged)
            {
                PlayWithLimit(attackRangedClip, SfxChannel.CombatLow);
            }
            else if (attackMeleeClips != null && attackMeleeClips.Length > 0)
            {
                int frame = _unit != null ? _unit.CurrentHitFrame : 0;
                int index = Mathf.Clamp(frame, 0, attackMeleeClips.Length - 1);
                PlayWithLimit(attackMeleeClips[index], SfxChannel.CombatLow);
            }
        }

        /// <summary>Client 端播放受击音效</summary>
        public void PlayHitNetwork()
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;

            if (hitClip != null)
                audio.PlayCombat(hitClip, hitVolumeScale * volumeScale);
        }

        /// <summary>Client 端播放死亡音效</summary>
        public void PlayDeathNetwork()
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;

            if (deathClip != null)
                audio.PlayCombatHigh(deathClip, volumeScale);
        }

        #endregion

        #region 公共方法 - 技能音效

        public void PlayCharge() => PlayWithLimit(chargeClip, SfxChannel.CombatLow);
        public void PlayShockwave() => PlayWithLimit(shockwaveClip, SfxChannel.CombatLow);
        public void PlaySplash() => PlayWithLimit(splashClip, SfxChannel.CombatLow);
        public void PlayStunHit() => PlayWithLimit(stunHitClip, SfxChannel.CombatLow);
        public void PlayKingAura() => PlayWithLimit(kingAuraClip, SfxChannel.CombatLow);
        public void PlayBurn() => PlayWithLimit(burnClip, SfxChannel.CombatLow);
        public void PlaySummon() => PlayWithLimit(summonClip, SfxChannel.CombatLow);
        public void PlayTaunt() => PlayWithLimit(tauntClip, SfxChannel.CombatLow);
        public void PlayShieldWall() => PlayWithLimit(shieldWallClip, SfxChannel.CombatLow);

        // ── 命中附加状态（流血 / 燃烧 / 损伤 / 重击 / 过热）──
        public void PlayBleed() => PlayWithLimit(bleedClip, SfxChannel.CombatLow);
        public void PlayIgnite() => PlayWithLimit(igniteClip, SfxChannel.CombatLow);
        public void PlayImpair() => PlayWithLimit(impairClip, SfxChannel.CombatLow);
        public void PlayHeavyStrike() => PlayWithLimit(heavyStrikeClip, SfxChannel.CombatLow);
        public void PlayOverheat() => PlayWithLimit(overheatClip, SfxChannel.CombatLow);

        // ── 其余缺失表现（护盾吸收 / 减速光环 / 连击 / 处决 / 飞扑 / 撕裂 / 治疗）──
        public void PlayShieldAbsorb() => PlayWithLimit(shieldAbsorbClip, SfxChannel.CombatLow);
        public void PlaySlowAura() => PlayWithLimit(slowAuraClip, SfxChannel.CombatLow);
        public void PlayLeader() => PlayWithLimit(leaderClip, SfxChannel.CombatLow);
        public void PlayBurst() => PlayWithLimit(burstClip, SfxChannel.CombatLow);
        public void PlayPounce() => PlayWithLimit(pounceClip, SfxChannel.CombatLow);
        public void PlayTear() => PlayWithLimit(tearClip, SfxChannel.CombatLow);
        public void PlayHeal() => PlayWithLimit(healClip, SfxChannel.CombatLow);

        /// <summary>处决走高优先级通道，确保能听清</summary>
        public void PlayExecute()
        {
            var audio = AudioManager.Instance;
            if (audio == null || executeClip == null) return;
            audio.PlayCombatHigh(executeClip, volumeScale);
        }

        public void PlayDeathExplosion()
        {
            // 死亡爆炸走高优先级，不受并发限制
            var audio = AudioManager.Instance;
            if (audio == null || deathExplosionClip == null) return;
            audio.PlayCombatHigh(deathExplosionClip, volumeScale);
        }

        #endregion

        #region 公共方法 - 通用播放

        public enum SfxChannel { UI, CombatHigh, Combat, CombatLow }

        /// <summary>
        /// 播放指定音效剪辑（走战斗普通通道，受并发限制）。
        /// </summary>
        public void PlayClip(AudioClip clip, float? customVolumeScale = null)
        {
            PlayWithLimit(clip, SfxChannel.Combat, customVolumeScale);
        }

        /// <summary>
        /// 指定通道播放音效（不受并发限制）。
        /// </summary>
        public void PlayClipWithChannel(AudioClip clip, SfxChannel channel, float? customVolumeScale = null)
        {
            if (clip == null) return;
            var audio = AudioManager.Instance;
            if (audio == null) return;

            float vol = customVolumeScale ?? volumeScale;
            switch (channel)
            {
                case SfxChannel.UI: audio.PlayUI(clip, vol); break;
                case SfxChannel.CombatHigh: audio.PlayCombatHigh(clip, vol); break;
                case SfxChannel.Combat: audio.PlayCombat(clip, vol); break;
                case SfxChannel.CombatLow: audio.PlayCombatLow(clip, vol); break;
            }
        }

        /// <summary>
        /// 兼容旧接口：3D 位置音效（2D 游戏改走战斗普通通道）。
        /// </summary>
        public void PlayClipAtPosition(AudioClip clip)
        {
            PlayWithLimit(clip, SfxChannel.Combat);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 播放音效，受按 Clip 分组并发限制 + 总并发限制 + 可见性检查。
        /// </summary>
        private void PlayWithLimit(AudioClip clip, SfxChannel channel, float? customVolumeScale = null)
        {
            if (clip == null) return;
            if (!CanPlay()) return;

            // 按 Clip 分组计数检查
            _clipCounts.TryGetValue(clip, out int clipCount);
            if (clipCount >= maxPerClipConcurrent) return;

            var audio = AudioManager.Instance;
            if (audio == null) return;

            // 递增计数
            _clipCounts[clip] = clipCount + 1;
            _pendingClips.Add(clip);

            float vol = customVolumeScale ?? volumeScale;
            switch (channel)
            {
                case SfxChannel.UI: audio.PlayUI(clip, vol); break;
                case SfxChannel.CombatHigh: audio.PlayCombatHigh(clip, vol); break;
                case SfxChannel.Combat: audio.PlayCombat(clip, vol); break;
                case SfxChannel.CombatLow: audio.PlayCombatLow(clip, vol); break;
            }

            // 延迟重置计数
            float delay = Mathf.Min(clip.length, 0.5f);
            StartCoroutine(DecrementClipCount(clip, delay));
        }

        /// <summary>
        /// 检查当前兵种是否在屏幕内（Viewport 可见性）。
        /// </summary>
        private bool CanPlay()
        {
            if (!cullOffScreen) return true;
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) return true;

            Vector3 vp = _mainCam.WorldToViewportPoint(transform.position);
            return vp.z > 0
                && vp.x >= -ViewportMargin && vp.x <= 1f + ViewportMargin
                && vp.y >= -ViewportMargin && vp.y <= 1f + ViewportMargin;
        }

        private System.Collections.IEnumerator DecrementClipCount(AudioClip clip, float delay)
        {
            yield return new WaitForSeconds(delay);

            _pendingClips.Remove(clip);
            if (_clipCounts.TryGetValue(clip, out int count))
            {
                if (count <= 1) _clipCounts.Remove(clip);
                else _clipCounts[clip] = count - 1;
            }
        }

        #endregion
    }
}
