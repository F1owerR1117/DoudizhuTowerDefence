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

        #endregion

        #region 私有字段

        /// <summary>关联的 CardUnit 组件</summary>
        private CardUnit _unit;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _unit = GetComponent<CardUnit>();
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

        #endregion
    }
}
