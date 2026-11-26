using DoudizhuTower.Gameplay.Systems;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 兵种特效组件：管理兵种的所有视觉特效。
    /// 通过 VFXManager 的对象池服务生成和回收特效。
    ///
    /// 使用方法：
    /// 1. 挂载到兵种预制体上（与 CardUnit 同级或子物体）
    /// 2. 在 Inspector 中为每个兵种配置独立的特效预制体
    /// 3. 留空则该特效不播放
    /// </summary>
    [RequireComponent(typeof(CardUnit))]
    public class UnitVFX : MonoBehaviour
    {
        #region 特效配置

        [Header("-- 攻击特效 --")]
        [Tooltip("溅射攻击爆炸特效（留空则不播放）")]
        [SerializeField] private GameObject splashExplosionVFX;
        [Tooltip("溅射特效持续时间（秒）")]
        [SerializeField] private float splashDuration = 0.75f;

        [Tooltip("治疗特效（留空则不播放）")]
        [SerializeField] private GameObject healVFX;
        [Tooltip("治疗特效持续时间（秒）")]
        [SerializeField] private float healDuration = 0.6f;

        [Header("-- 技能特效 --")]
        [Tooltip("冲锋特效（留空则不播放）")]
        [SerializeField] private GameObject chargeVFX;
        [Tooltip("冲锋特效持续时间（秒）")]
        [SerializeField] private float chargeDuration = 0.5f;

        [Tooltip("震波特效（留空则不播放）")]
        [SerializeField] private GameObject shockwaveVFX;
        [Tooltip("震波特效持续时间（秒）")]
        [SerializeField] private float shockwaveDuration = 0.75f;

        [Tooltip("眩晕命中特效（留空则不播放）")]
        [SerializeField] private GameObject stunHitVFX;
        [Tooltip("眩晕特效持续时间（秒）")]
        [SerializeField] private float stunHitDuration = 0.75f;

        [Tooltip("君王光环特效（留空则不播放）")]
        [SerializeField] private GameObject kingAuraVFX;
        [Tooltip("君王光环持续时间（秒，0=跟随目标生命周期）")]
        [SerializeField] private float kingAuraDuration = 0f;

        [Tooltip("死亡爆炸特效（留空则不播放）")]
        [SerializeField] private GameObject deathExplosionVFX;
        [Tooltip("死亡爆炸持续时间（秒）")]
        [SerializeField] private float deathExplosionDuration = 1f;

        [Tooltip("燃烧特效（留空则不播放）")]
        [SerializeField] private GameObject burnVFX;
        [Tooltip("燃烧特效持续时间（秒）")]
        [SerializeField] private float burnDuration = 3f;

        [Tooltip("召唤特效（留空则不播放）")]
        [SerializeField] private GameObject summonVFX;
        [Tooltip("召唤特效持续时间（秒）")]
        [SerializeField] private float summonDuration = 1f;

        [Header("-- 生成特效 --")]
        [Tooltip("生成特效（留空则不播放）")]
        [SerializeField] private GameObject spawnVFX;
        [Tooltip("生成特效持续时间（秒）")]
        [SerializeField] private float spawnDuration = 0.75f;
        [Tooltip("生成音效（留空则不播放）")]
        [SerializeField] private AudioClip spawnSound;
        [Tooltip("生成音效音量")]
        [SerializeField] private float spawnSoundVolume = 0.8f;

        [Header("-- 状态特效 --")]
        [Tooltip("护盾特效（留空则不播放）")]
        [SerializeField] private GameObject shieldVFX;
        [Tooltip("嘲讽光环特效（留空则不播放）")]
        [SerializeField] private GameObject tauntAuraVFX;
        [Tooltip("撕裂特效（留空则不播放）")]
        [SerializeField] private GameObject tearVFX;
        [Tooltip("撕裂特效持续时间（秒）")]
        [SerializeField] private float tearDuration = 0.75f;

        [Header("-- 命中附加状态特效 --")]
        [Tooltip("流血特效（留空则不播放）")]
        [SerializeField] private GameObject bleedVFX;
        [Tooltip("流血特效持续时间（秒）")]
        [SerializeField] private float bleedDuration = 0.75f;

        [Tooltip("燃烧（点燃）特效（留空则不播放）")]
        [SerializeField] private GameObject igniteVFX;
        [Tooltip("点燃特效持续时间（秒）")]
        [SerializeField] private float igniteDuration = 0.75f;

        [Tooltip("损伤（减速）特效（留空则不播放）")]
        [SerializeField] private GameObject impairVFX;
        [Tooltip("损伤特效持续时间（秒）")]
        [SerializeField] private float impairDuration = 0.6f;

        [Tooltip("重击（击退）特效（留空则不播放）")]
        [SerializeField] private GameObject heavyStrikeVFX;
        [Tooltip("重击特效持续时间（秒）")]
        [SerializeField] private float heavyStrikeDuration = 0.5f;

        [Tooltip("过热光环特效（留空则不播放，持续跟随自身）")]
        [SerializeField] private GameObject overheatVFX;

        [Header("-- 光环与状态特效 --")]
        [Tooltip("减速光环特效（留空则不播放，持续跟随自身）")]
        [SerializeField] private GameObject slowAuraVFX;
        [Tooltip("人海增伤光环特效（留空则不播放，友军存在时跟随自身）")]
        [SerializeField] private GameObject swarmAuraVFX;
        [Tooltip("领袖光环特效（留空则不播放，持续跟随自身）")]
        [SerializeField] private GameObject leaderAuraVFX;
        [Tooltip("快速连击爆发特效（留空则不播放）")]
        [SerializeField] private GameObject burstVFX;
        [Tooltip("快速连击特效持续时间（秒）")]
        [SerializeField] private float burstDuration = 0.5f;

        [Tooltip("处决特效（留空则不播放）")]
        [SerializeField] private GameObject executeVFX;
        [Tooltip("处决特效持续时间（秒）")]
        [SerializeField] private float executeDuration = 0.75f;

        [Tooltip("飞扑特效（留空则不播放，起跳与落地各播一次）")]
        [SerializeField] private GameObject pounceVFX;
        [Tooltip("飞扑特效持续时间（秒）")]
        [SerializeField] private float pounceDuration = 0.5f;

        [Tooltip("点杀锁定标记特效（留空则不播放，锁定时挂在目标身上）")]
        [SerializeField] private GameObject sniperMarkVFX;
        [Tooltip("锁定标记持续时间（秒）")]
        [SerializeField] private float sniperMarkDuration = 1f;

        #endregion

        #region 私有字段

        /// <summary>关联的 CardUnit 组件</summary>
        private CardUnit _unit;

        /// <summary>过热光环实例（用于关闭时销毁）</summary>
        private GameObject _overheatAura;

        /// <summary>减速光环实例</summary>
        private GameObject _slowAura;

        /// <summary>人海光环实例</summary>
        private GameObject _swarmAura;

        /// <summary>领袖光环实例</summary>
        private GameObject _leaderAura;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _unit = GetComponent<CardUnit>();
        }

        private void OnDisable()
        {
            // 回池时清掉持续跟随的光环，避免残留
            SetOverheatAura(false);
            SetSlowAura(false);
            SetSwarmAura(false);
            SetLeaderAura(false);
        }

        #endregion

        #region 公共方法 - 攻击特效

        /// <summary>
        /// 播放溅射攻击爆炸特效。
        /// 由 UnitPassives 调用。
        /// </summary>
        /// <param name="position">爆炸位置</param>
        /// <param name="radius">爆炸半径（用于缩放特效）</param>
        public void PlaySplash(Vector3 position, float radius = 1f)
        {
            var vfx = VFXManager.Instance?.SpawnVFX(splashExplosionVFX, position, null, splashDuration);
            if (vfx != null)
            {
                float scale = radius / 2f;
                vfx.transform.localScale = Vector3.one * scale;
            }
        }

        #endregion

        #region 公共方法 - 技能特效

        /// <summary>
        /// 播放冲锋特效（跟随目标）。
        /// 由 UnitPassives 调用。
        /// </summary>
        /// <param name="target">冲锋目标</param>
        /// <param name="duration">持续时间</param>
        public void PlayCharge(Transform target, float duration = -1f)
        {
            if (duration < 0f) duration = chargeDuration;
            var vfx = VFXManager.Instance?.SpawnVFX(chargeVFX, target.position, target, 0f, false);
            if (vfx != null)
            {
                var follower = vfx.AddComponent<FollowTarget>();
                follower.Initialize(target, Vector3.zero);
                Destroy(vfx, duration);
            }
        }

        /// <summary>
        /// 播放震波特效。
        /// 由 UnitPassives 调用。
        /// </summary>
        /// <param name="position">震波位置</param>
        /// <param name="radius">震波半径（用于缩放特效）</param>
        public void PlayShockwave(Vector3 position, float radius = 3f)
        {
            var vfx = VFXManager.Instance?.SpawnVFX(shockwaveVFX, position, null, shockwaveDuration);
            if (vfx != null)
            {
                float scale = radius / 2f;
                vfx.transform.localScale = Vector3.one * scale;
            }
        }

        /// <summary>
        /// 播放治疗特效（挂在目标身上，跟随移动）。
        /// 由 UnitPassives 调用。
        /// </summary>
        /// <param name="target">被治疗的友方 Transform</param>
        public void PlayHeal(Transform target)
        {
            if (target == null) return;
            VFXManager.Instance?.SpawnVFX(healVFX, target.position, target, healDuration);
        }

        /// <summary>
        /// 播放眩晕命中特效（挂在目标身上，跟随移动）。
        /// 由 UnitPassives 调用。
        /// </summary>
        /// <param name="target">目标 Transform（特效跟随）</param>
        public void PlayStunHit(Transform target)
        {
            if (target == null) return;
            VFXManager.Instance?.SpawnVFX(stunHitVFX, target.position, target, stunHitDuration);
        }

        /// <summary>
        /// 播放君王光环特效（跟随释放者）。
        /// 由 UnitPassives 调用。
        /// </summary>
        /// <param name="target">跟随目标</param>
        /// <param name="radius">光环半径（用于缩放特效）</param>
        public void PlayKingAura(Transform target, float radius = 3f)
        {
            var vfx = VFXManager.Instance?.SpawnVFX(kingAuraVFX, target.position, target, kingAuraDuration);
            if (vfx != null)
            {
                float scale = radius / 2f;
                vfx.transform.localScale = Vector3.one * scale;
                var follower = vfx.AddComponent<FollowTarget>();
                follower.Initialize(target, Vector3.zero);
            }
        }

        /// <summary>
        /// 播放死亡爆炸特效。
        /// 由 UnitPassives 调用。
        /// </summary>
        /// <param name="position">爆炸位置</param>
        /// <param name="radius">爆炸半径（用于缩放特效）</param>
        public void PlayDeathExplosion(Vector3 position, float radius = 2f)
        {
            var vfx = VFXManager.Instance?.SpawnVFX(deathExplosionVFX, position, null, deathExplosionDuration);
            if (vfx != null)
            {
                float scale = radius / 2f;
                vfx.transform.localScale = Vector3.one * scale;
            }
        }

        /// <summary>
        /// 播放燃烧特效。
        /// 由 UnitPassives 调用。
        /// </summary>
        /// <param name="position">燃烧位置</param>
        /// <param name="radius">火海半径（用于缩放特效）</param>
        /// <param name="duration">持续时间</param>
        public void PlayBurn(Vector3 position, float radius = 2f, float duration = -1f)
        {
            if (duration < 0f) duration = burnDuration;
            var vfx = VFXManager.Instance?.SpawnVFX(burnVFX, position, null, duration);
            if (vfx != null)
            {
                float scale = radius / 2f;
                vfx.transform.localScale = Vector3.one * scale;
            }
        }

        /// <summary>
        /// 播放召唤特效（生成在召唤位置）。
        /// 由 UnitPassives 调用。
        /// </summary>
        /// <param name="position">召唤位置</param>
        public void PlaySummon(Vector3 position)
        {
            VFXManager.Instance?.SpawnVFX(summonVFX, position, null, summonDuration);
        }

        /// <summary>
        /// 播放生成特效和音效。在兵种从对象池生成时调用。
        /// </summary>
        public void PlaySpawn()
        {
            VFXManager.Instance?.SpawnVFX(spawnVFX, _unit.VisualCenter, null, spawnDuration);
            if (spawnSound != null)
                AudioManager.Instance?.PlayCombatLow(spawnSound, spawnSoundVolume);
        }

        #endregion

        #region 公共方法 - 状态特效

        /// <summary>
        /// 生成护盾特效（持续跟随目标）。
        /// 由 UnitPassives 调用。
        /// </summary>
        /// <param name="target">跟随目标</param>
        /// <param name="duration">持续时间（0=永久，需手动销毁）</param>
        /// <returns>特效实例（用于手动销毁）</returns>
        public GameObject SpawnShield(Transform target, float duration = 0f)
        {
            var vfx = VFXManager.Instance?.SpawnVFX(shieldVFX, target.position, target, 0f, false);
            if (vfx != null)
            {
                var follower = vfx.AddComponent<FollowTarget>();
                follower.Initialize(target, Vector3.zero);
                if (duration > 0f)
                    Destroy(vfx, duration);
            }
            return vfx;
        }

        /// <summary>
        /// 生成嘲讽光环特效（持续跟随目标）。
        /// 由 UnitPassives 调用。
        /// </summary>
        /// <param name="target">跟随目标</param>
        /// <param name="radius">光环半径（用于缩放特效）</param>
        /// <returns>特效实例</returns>
        public GameObject SpawnTauntAura(Transform target, float radius = 3f)
        {
            var vfx = VFXManager.Instance?.SpawnVFX(tauntAuraVFX, target.position, target, 0f, false);
            if (vfx != null)
            {
                float scale = radius / 2f;
                vfx.transform.localScale = Vector3.one * scale;
                var follower = vfx.AddComponent<FollowTarget>();
                follower.Initialize(target, Vector3.zero);
            }
            return vfx;
        }

        /// <summary>
        /// 播放撕裂特效（挂在目标身上，跟随移动）。
        /// 由 UnitPassives 调用。
        /// </summary>
        /// <param name="target">目标 Transform（特效跟随）</param>
        public void PlayTear(Transform target)
        {
            if (target == null) return;
            VFXManager.Instance?.SpawnVFX(tearVFX, target.position, target, tearDuration);
        }

        /// <summary>播放流血特效（挂在目标身上）</summary>
        public void PlayBleed(Transform target)
        {
            if (target == null) return;
            VFXManager.Instance?.SpawnVFX(bleedVFX, target.position, target, bleedDuration);
        }

        /// <summary>播放点燃特效（挂在目标身上）</summary>
        public void PlayIgnite(Transform target)
        {
            if (target == null) return;
            VFXManager.Instance?.SpawnVFX(igniteVFX, target.position, target, igniteDuration);
        }

        /// <summary>播放损伤（减速）特效（挂在目标身上）</summary>
        public void PlayImpair(Transform target)
        {
            if (target == null) return;
            VFXManager.Instance?.SpawnVFX(impairVFX, target.position, target, impairDuration);
        }

        /// <summary>播放重击特效（击退方向上的冲击）</summary>
        public void PlayHeavyStrike(Vector3 position, float distance = 1.5f)
        {
            var vfx = VFXManager.Instance?.SpawnVFX(heavyStrikeVFX, position, null, heavyStrikeDuration);
            if (vfx != null)
            {
                float scale = Mathf.Max(distance / 2f, 0.5f);
                vfx.transform.localScale = Vector3.one * scale;
            }
        }

        /// <summary>
        /// 开关过热光环（持续跟随自身）。加成生效时开启，清零时关闭。
        /// </summary>
        public void SetOverheatAura(bool on)
        {
            if (on)
            {
                if (_overheatAura != null) return;
                if (overheatVFX == null) return;
                var vfx = VFXManager.Instance?.SpawnVFX(overheatVFX, _unit.VisualCenter, transform, 0f, false);
                if (vfx != null)
                {
                    var follower = vfx.AddComponent<FollowTarget>();
                    follower.Initialize(transform, Vector3.zero);
                    _overheatAura = vfx;
                }
            }
            else if (_overheatAura != null)
            {
                Destroy(_overheatAura);
                _overheatAura = null;
            }
        }

        /// <summary>开关减速光环（持续跟随自身，按半径缩放）</summary>
        public void SetSlowAura(bool on, float radius = 3f)
        {
            if (on)
            {
                if (_slowAura != null || slowAuraVFX == null) return;
                var vfx = VFXManager.Instance?.SpawnVFX(slowAuraVFX, _unit.VisualCenter, transform, 0f, false);
                if (vfx != null)
                {
                    vfx.transform.localScale = Vector3.one * Mathf.Max(radius / 2f, 0.1f);
                    var follower = vfx.AddComponent<FollowTarget>();
                    follower.Initialize(transform, Vector3.zero);
                    _slowAura = vfx;
                }
            }
            else if (_slowAura != null)
            {
                Destroy(_slowAura);
                _slowAura = null;
            }
        }

        /// <summary>开关人海光环（有友军时显示，按半径缩放）</summary>
        public void SetSwarmAura(bool on, float radius = 2f)
        {
            if (on)
            {
                if (_swarmAura != null || swarmAuraVFX == null) return;
                var vfx = VFXManager.Instance?.SpawnVFX(swarmAuraVFX, _unit.VisualCenter, transform, 0f, false);
                if (vfx != null)
                {
                    vfx.transform.localScale = Vector3.one * Mathf.Max(radius / 2f, 0.1f);
                    var follower = vfx.AddComponent<FollowTarget>();
                    follower.Initialize(transform, Vector3.zero);
                    _swarmAura = vfx;
                }
            }
            else if (_swarmAura != null)
            {
                Destroy(_swarmAura);
                _swarmAura = null;
            }
        }

        /// <summary>开关领袖光环（持续跟随自身，按半径缩放）</summary>
        public void SetLeaderAura(bool on, float radius = 4f)
        {
            if (on)
            {
                if (_leaderAura != null || leaderAuraVFX == null) return;
                var vfx = VFXManager.Instance?.SpawnVFX(leaderAuraVFX, _unit.VisualCenter, transform, 0f, false);
                if (vfx != null)
                {
                    vfx.transform.localScale = Vector3.one * Mathf.Max(radius / 2f, 0.1f);
                    var follower = vfx.AddComponent<FollowTarget>();
                    follower.Initialize(transform, Vector3.zero);
                    _leaderAura = vfx;
                }
            }
            else if (_leaderAura != null)
            {
                Destroy(_leaderAura);
                _leaderAura = null;
            }
        }

        /// <summary>播放快速连击爆发特效（自身位置）</summary>
        public void PlayBurst(Transform self)
        {
            if (self == null) return;
            VFXManager.Instance?.SpawnVFX(burstVFX, self.position, self, burstDuration);
        }

        /// <summary>播放处决特效（被处决目标位置）</summary>
        public void PlayExecute(Transform target)
        {
            if (target == null) return;
            VFXManager.Instance?.SpawnVFX(executeVFX, target.position, target, executeDuration);
        }

        /// <summary>播放飞扑特效（起跳或落地位置）</summary>
        public void PlayPounce(Vector3 position)
        {
            VFXManager.Instance?.SpawnVFX(pounceVFX, position, null, pounceDuration);
        }

        /// <summary>播放点杀锁定标记（挂在目标身上）</summary>
        public void PlaySniperMark(Transform target)
        {
            if (target == null) return;
            VFXManager.Instance?.SpawnVFX(sniperMarkVFX, target.position, target, sniperMarkDuration);
        }

        #endregion
    }
}
