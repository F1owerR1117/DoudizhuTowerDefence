using DoudizhuTower.Core.Battle;
using DoudizhuTower.Gameplay.Battle;
using DoudizhuTower.Gameplay.Systems;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    public class Projectile : MonoBehaviour
    {
        [Header("飞行参数")]
        [Header("子弹速度")]
        [SerializeField] private float speed = 8f;
        [Header("抛物线弹道")]
        [SerializeField] private bool _useParabolic;
        [Header("抛物线高度")]
        [SerializeField] private float _arcHeight = 3f;
        [Header("最大存活时间（秒）")]
        [SerializeField] private float _maxLifetime = 5f;
        [Header("射击者死亡时销毁")]
        [SerializeField] private bool _destroyOnShooterDeath = true;
        [Header("目标死亡时销毁")]
        [SerializeField] private bool _destroyOnTargetDeath = true;

        [Header("命中效果")]
        [Header("爆炸半径（0=单目标）")]
        [SerializeField] private float _explosionRadius;
        [Tooltip("子弹命中特效（留空则不播放）")]
        [SerializeField] private GameObject bulletHitVFX;
        [Tooltip("子弹爆炸特效（留空则不播放）")]
        [SerializeField] private GameObject bulletExplosionVFX;
        [Tooltip("子弹命中音效（留空则不播放）")]
        [SerializeField] private AudioClip hitSound;
        [Tooltip("命中音效音量")]
        [Range(0f, 1f)]
        [SerializeField] private float hitSoundVolume = 0.8f;

        private CardUnit _target;
        private CardUnit _shooter;
        private bool _shooterIsLandlord;
        private IBuildingTarget _buildingTarget;
        private float _damage;
        private DamageType _damageType;
        private SpriteRenderer _sr;
        private Vector3 _startPos;
        private float _launchTime;
        private float _totalDist;
        private bool _hasHit;
        // 爆炸检测缓冲区（避免每帧分配）
        private static readonly Collider2D[] _explosionBuffer = new Collider2D[32];
        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _startPos = transform.position;
            _launchTime = Time.time;
        }

        private void OnDrawGizmosSelected()
        {
            if (_explosionRadius > 0f)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, _explosionRadius);
            }
        }

        public void Fire(CardUnit shooter, CardUnit target, float damage, DamageType damageType, Sprite sprite = null)
        {
            _shooter = shooter;
            _shooterIsLandlord = shooter != null && shooter.IsLandlord;
            _target = (target != null && target != shooter) ? target : null;
            _buildingTarget = null;
            _damage = damage;
            _damageType = damageType;
            if (_sr != null && sprite != null) _sr.sprite = sprite;
        }

        public void FireAtBuilding(CardUnit shooter, IBuildingTarget building, float damage)
        {
            _shooter = shooter;
            _shooterIsLandlord = shooter != null && shooter.IsLandlord;
            _buildingTarget = building;
            _target = null;
            _damage = damage;
            _damageType = DamageType.Physical;
        }

        private void Update()
        {

            bool targetDead = (_destroyOnTargetDeath && _target != null && !_target.IsAlive)
                           || (_buildingTarget != null && _buildingTarget.IsDestroyed);
            if (targetDead) { Destroy(gameObject); return; }
            if (_destroyOnShooterDeath && (_shooter == null || !_shooter.IsAlive)) { Destroy(gameObject); return; }
            if (Time.time - _launchTime > _maxLifetime) { Destroy(gameObject); return; }

            if (_useParabolic)
                ParabolicUpdate();
            else
                LinearUpdate();
        }

        private Vector3 GetTargetPos()
        {
            if (_target != null)
            {
                var col = _target.Collider2D;
                if (col != null) return col.ClosestPoint(transform.position);
                return _target.VisualCenter;
            }
            if (_buildingTarget != null)
            {
                var col = _buildingTarget.BuildingCollider;
                if (col != null) return col.ClosestPoint(transform.position);
                return _buildingTarget.LogicCenter;
            }
            return transform.position + transform.right;
        }

        private void LinearUpdate()
        {
            Vector3 dir = GetTargetPos() - transform.position;
            float dist = dir.magnitude;

            RotateToward(dir);

            if (CheckHit(dist)) return;
            transform.Translate(dir.normalized * speed * Time.deltaTime, Space.World);
        }

        private void ParabolicUpdate()
        {
            Vector3 targetPos = GetTargetPos();
            if (_totalDist <= 0f)
                _totalDist = Vector3.Distance(_startPos, targetPos);

            float elapsed = Time.time - _launchTime;
            float totalTime = _totalDist / Mathf.Max(speed, 0.1f);
            float t = Mathf.Clamp01(elapsed / totalTime);

            Vector3 basePos = Vector3.Lerp(_startPos, targetPos, t);
            basePos.y += _arcHeight * 4f * t * (1f - t);
            transform.position = basePos;

            float distToTarget = Vector3.Distance(transform.position, targetPos);
            if (CheckHit(distToTarget)) return;
            if (t >= 1f) { Hit(); return; }
        }

        /// <summary>
        /// 边缘命中检测：优先用 ClosestPoint 判定弹体是否触及目标碰撞箱边缘，
        /// 回退到中心距离检测（dist &lt; 0.5f）。
        /// 大体积单位下，弹体触边即中，不再需要飞到中心点。
        /// </summary>
        private bool CheckHit(float distFallback)
        {
            // 1. CardUnit 目标的边缘检测
            if (_target != null && _target.IsAlive)
            {
                var col = _target.Collider2D;
                if (col != null)
                {
                    Vector3 closest = col.ClosestPoint(transform.position);
                    if (Vector3.Distance(transform.position, closest) < 0.5f)
                    {
                        Hit(); return true;
                    }
                }
            }

            // 2. 建筑目标的边缘检测
            if (_buildingTarget != null && !_buildingTarget.IsDestroyed)
            {
                var col = _buildingTarget.transform.GetComponent<Collider2D>();
                if (col != null)
                {
                    Vector3 closest = col.ClosestPoint(transform.position);
                    if (Vector3.Distance(transform.position, closest) < 0.5f)
                    {
                        Hit(); return true;
                    }
                }
            }

            // 3. 回退：中心距检测
            if (distFallback < 0.5f) { Hit(); return true; }
            return false;
        }

        private void RotateToward(Vector3 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        private void Hit()
        {
            if (_hasHit) return;
            _hasHit = true;

            // 播放命中音效
            if (hitSound != null)
                AudioManager.Instance?.PlayCombatLow(hitSound, hitSoundVolume);

            // 使用 VFXManager 播放子弹命中特效
            var vfxManager = VFXManager.Instance;
            if (vfxManager != null && bulletHitVFX != null)
            {
                // 计算命中法线（从射击者指向命中点的方向）
                Vector3 hitNormal = _shooter != null
                    ? (transform.position - _shooter.transform.position).normalized
                    : Vector3.forward;
                var hitVfx = vfxManager.SpawnVFX(bulletHitVFX, transform.position);
                if (hitVfx != null && hitNormal != Vector3.zero)
                    hitVfx.transform.rotation = Quaternion.LookRotation(hitNormal);
            }

            // ── 主目标必中伤害 ──
            if (_buildingTarget != null && !_buildingTarget.IsDestroyed)
                _buildingTarget.TakeDamage(_damage);
            else if (_target != null && _target.IsAlive)
            {
                _target.LastAttacker = _shooter;
                _target.TakeDamage(_damage, _damageType, _shooter);
            }

            // ── 爆炸（额外范围伤害，不重复伤害主目标） ──
            if (_explosionRadius > 0f)
            {
                // 播放爆炸特效
                if (vfxManager != null && bulletExplosionVFX != null)
                {
                    var explosionVfx = vfxManager.SpawnVFX(bulletExplosionVFX, transform.position);
                    if (explosionVfx != null)
                    {
                        float scale = _explosionRadius / 2f;
                        explosionVfx.transform.localScale = Vector3.one * scale;
                    }
                }

                // 使用缓存数组，避免分配
                var filter = new ContactFilter2D().NoFilter();
                int count = Physics2D.OverlapCircle((Vector2)transform.position, _explosionRadius, filter, _explosionBuffer);
                for (int i = 0; i < count; i++)
                {
                    var unit = _explosionBuffer[i].GetComponentInParent<CardUnit>();
                    if (unit != null && unit.IsAlive)
                    {
                        bool isSelf = unit == _shooter;
                        bool isMainTarget = unit == _target;
                        bool isEnemy = unit.IsLandlord != _shooterIsLandlord;
                        bool canHitHeight = _shooter == null || _shooter.CanAttackHeight(unit.UnitHeight);
                        if (!isSelf && !isMainTarget && isEnemy && canHitHeight)
                        {
                            unit.TakeDamage(_damage, _damageType, _shooter);
                        }
                    }

                    var buildingTarget = _explosionBuffer[i].GetComponentInParent<CardUnit>() as IBuildingTarget;
                    if (buildingTarget != null && !buildingTarget.IsDestroyed
                        && buildingTarget != _buildingTarget
                        && buildingTarget.transform != _shooter?.transform)
                    {
                        var buildingUnit = buildingTarget as CardUnit;
                        if (buildingUnit != null && buildingUnit.IsLandlord != _shooterIsLandlord)
                            buildingTarget.TakeDamage(_damage);
                    }
                }
            }

            Destroy(gameObject);
        }
    }
}
