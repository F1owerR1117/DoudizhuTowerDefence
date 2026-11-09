using System.Collections;
using System.Collections.Generic;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Gameplay.Battle;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 兵种被动系统（partial class）。
    /// UnitPassives.cs        — 字段、生命周期、光环、战斗被动
    /// UnitPassives.Summon.cs — 召唤师被动（定时召唤 + 击杀召唤）
    /// </summary>
    public partial class UnitPassives : MonoBehaviour
    {
        [Header("══════ 通用被动（Inspector 中勾选启用）══════")]

        [Header("点杀")]
        [Tooltip("启用后自动锁定全场血量最低的敌方单位")]
        public bool enableSniper;
        [Tooltip("点杀搜索范围（0 = 使用索敌范围）。低血量目标可在此范围内被锁定。")]
        public float sniperRangeBonus;
        [Range(0f, 1f)]
        [Tooltip("超出攻击范围时只攻击血量低于此百分比的目标")]
        public float sniperHpThreshold = 0.3f;

        [Header("人海连击")]
        [Tooltip("启用后攻击时周围每名友军追加一定比例的伤害")]
        public bool enableSwarm;
        [Tooltip("感知友军的半径")]
        public float swarmRadius = 2f;
        [Tooltip("每个友军追加的伤害百分比 (0.5 = +50%)")]
        public float swarmDamagePct = 0.5f;

        [Header("冲锋一击")]
        [Tooltip("启用后蓄力后首击造成高额伤害")]
        public bool enableCharge;
        [Tooltip("冲锋伤害倍率 (2.5 = ATK×2.5)")]
        public float chargeMultiplier = 2.5f;
        [Tooltip("重新冲锋所需冷却时间（秒）")]
        public float chargeCooldown = 6f;
        [Tooltip("冲锋期间的移动速度倍率")]
        public float chargeSpeedMultiplier = 1.3f;
        [Tooltip("冲锋期间覆盖高度标签（留空=不覆盖）")]
        public UnitHeight chargeHeightOverride = 0;
        [Tooltip("冲锋期间可被哪些高度阻挡（留空=与覆盖高度相同）")]
        public UnitHeight chargeBlockableByHeight = 0;

        [Header("君王光环")]
        [Tooltip("启用后每隔数秒震退周围敌军")]
        public bool enableKingAura;
        [Tooltip("震退间隔（秒）")]
        public float kingInterval = 5f;
        [Tooltip("震退影响半径")]
        public float kingRadius = 3f;
        [Tooltip("震退距离")]
        public float kingPushDistance = 1f;

        [Header("盾墙线")]
        [Tooltip("启用后为周围友军提供伤害减免")]
        public bool enableShieldWall;
        [Tooltip("影响半径")]
        public float shieldRange = 3f;
        [Tooltip("伤害减免百分比 (0.2 = 20%)")]
        public float shieldDamageReduction = 0.2f;

        [Header("嘲讽")]
        [Tooltip("启用后敌方单位在攻击范围内将优先攻击自己")]
        public bool enableTaunt;
        [Tooltip("嘲讽光环半径（敌方必须同时落在光环半径与攻击范围的交集内才生效）")]
        public float tauntRadius = 5f;

        [Header("死亡爆炸")]
        [Tooltip("启用后死亡时对周围敌方单位造成范围伤害")]
        public bool enableDeathExplosion;
        [Tooltip("爆炸半径")]
        public float explosionRadius = 2f;
        [Tooltip("爆炸伤害百分比 (1.0 = 100% ATK)")]
        public float explosionDamagePct = 1f;

        [Header("护盾吸收")]
        [Tooltip("启用后获得可吸收伤害的护盾值")]
        public bool enableShieldAbsorb;
        [Tooltip("护盾吸收量")]
        public float shieldAmount = 200f;

        [Header("减速光环")]
        [Tooltip("启用后周围敌方单位移速降低")]
        public bool enableSlowAura;
        [Tooltip("减速光环半径")]
        public float slowRadius = 3f;
        [Range(0f, 1f)]
        [Tooltip("减速百分比 (0.3 = 移速×0.7)")]
        public float slowPercent = 0.3f;
        [Tooltip("减速持续时间（秒）")]
        public float slowDuration = 2f;

        [Header("攻击眩晕")]
        [Tooltip("启用后攻击命中时眩晕目标")]
        public bool enableStunOnHit;
        [Tooltip("眩晕持续时间（秒）")]
        public float stunDuration = 1f;

        [Header("快速连击")]
        [Tooltip("启用后快速攻击 N 次后自我眩晕进入冷却")]
        public bool enableBurstAttack;
        [Tooltip("连击次数")]
        public int burstHitCount = 3;
        [Tooltip("自我眩晕冷却时间（秒）")]
        public float burstCooldown = 2f;
        private int _burstHitCounter;

        [Header("撕裂（易伤叠加）")]
        [Tooltip("启用后每次攻击为目标叠加易伤效果，受伤增加")]
        public bool enableTear;
        [Tooltip("每层易伤百分比 (0.05 = +5% 受伤)")]
        public float tearDamagePerStack = 0.05f;
        [Tooltip("最大叠加层数")]
        public int tearMaxStacks = 5;
        [Tooltip("每层持续时间（秒）")]
        public float tearDuration = 4f;

        [Header("出场震波")]
        [Tooltip("启用后出场时震退周围敌人并造成伤害")]
        public bool enableShockwave;
        [Tooltip("震波半径")]
        public float shockwaveRadius = 2f;
        [Tooltip("震波伤害百分比 (0.3 = 30% ATK)")]
        public float shockwaveDamagePct = 0.3f;

        [Header("死亡燃烧")]
        [Tooltip("启用后死亡时留下火海，对经过的敌人持续造成伤害")]
        public bool enableBurnOnDeath;
        [Tooltip("火海半径")]
        public float burnRadius = 2f;
        [Tooltip("每秒伤害百分比 (0.2 = 20% ATK/秒)")]
        public float burnDamagePct = 0.2f;
        [Tooltip("火海持续时间（秒）")]
        public float burnDuration = 3f;

        [Header("溅射攻击")]
        [Tooltip("启用后攻击时对目标周围敌方单位造成范围伤害")]
        public bool enableSplash;
        [Tooltip("溅射半径")]
        public float splashRadius = 2f;
        [Tooltip("溅射伤害百分比 (0.5 = 50% ATK)")]
        public float splashDamagePct = 0.5f;

        [Header("骑兵追远程")]
        [Tooltip("启用后优先攻击攻击距离大于等于指定值的敌方单位")]
        public bool enableCavalryChase;
        [Tooltip("触发追远程的最低攻击距离阈值")]
        public float cavalryChaseRangeThreshold = 8f;

        [Header("召唤师")]
        [Tooltip("启用后定时召唤单位，击杀敌人时也会召唤")]
        public bool enableSummoner;
        [Tooltip("召唤物预制体")]
        public GameObject summonPrefab;
        [Tooltip("定时召唤间隔（秒）")]
        public float summonInterval = 8f;
        [Tooltip("最大同时存活召唤物数量")]
        public int maxSummons = 5;
        [Tooltip("击杀敌人时是否召唤")]
        public bool summonOnKill = true;

        private CardUnit _owner;
        private UnitAudio _unitAudio;
        private UnitVFX _unitVFX;
        private bool _isCharged;
        private float _originalSpeed;
        private float _lastAttackTime = -999f;
        private bool _chargeAnimSynced;
        private float _summonTimer;
        private readonly System.Collections.Generic.List<CardUnit> _summons = new();
        private bool _isSummoning;
        private Vector3 _summonPosition;
        private float _kingTimer;
        private CardUnit _currentSniperTarget;

        private readonly Collider2D[] _overlapBuffer = new Collider2D[64];
        private static readonly ContactFilter2D _overlapFilter = new ContactFilter2D { useTriggers = true, useLayerMask = false };

        // 盾墙单位缓存：Awake 注册，OnDestroy 注销，ApplyShieldWallGlobal 直接遍历
        private static readonly List<UnitPassives> _shieldWallUnits = new();

        private void Awake()
        {
            _owner = GetComponentInParent<CardUnit>();
            if (_owner == null) return;

            // 获取 UnitAudio 组件（如果存在）
            _unitAudio = _owner.GetComponentInChildren<UnitAudio>();
            // 获取 UnitVFX 组件（如果存在）
            _unitVFX = _owner.GetComponent<UnitVFX>();

            // 盾墙单位注册到缓存
            if (enableShieldWall)
                _shieldWallUnits.Add(this);

            if (enableSniper)
            {
                _owner.OverrideFindTarget = FindSniperTarget;
                _owner.OverrideAttackRange = GetSniperAttackRange;
            }
            if (enableCavalryChase) _owner.OverrideFindTarget = FindCavalryChaseTarget;
            if (enableSwarm || enableCharge || enableStunOnHit || enableSplash || enableBurstAttack) _owner.OnAttackEvent += OnAttack;
            if (enableStunOnHit || enableSplash || enableBurstAttack) _owner.OnPerTargetAttackEvent += OnPerTargetAttack;
            if (enableTear) _owner.OnAttackEvent += OnTearAttack;
            if (enableDeathExplosion || enableBurnOnDeath) _owner.OnDeathEvent += OnDeath;
            if (enableSummoner && summonOnKill) _owner.OnKillEvent += OnSummonerKill;
            if (enableCharge)
            {
                _isCharged = true;
                _originalSpeed = _owner.Stats.MoveSpeed;
                ApplyChargeSpeed(true);
                if (chargeHeightOverride != 0)
                    _owner.SetHeightOverride(chargeHeightOverride, chargeBlockableByHeight);
                _unitAudio?.PlayCharge();
            }
            if (enableTaunt)
            {
                _owner.IsTauntSource = true;
                _owner.TauntRadius = tauntRadius;
                _owner.SetAnimBool("Taunt", true);
                _unitAudio?.PlayTaunt();
                _unitVFX?.SpawnTauntAura(_owner.transform, tauntRadius);
            }
            if (enableShieldWall)
            {
                _owner.SetAnimBool("ShieldWall", true);
                _unitAudio?.PlayShieldWall();
                _unitVFX?.SpawnShield(_owner.transform);
            }
            if (enableShieldAbsorb) _owner.DamageAbsorbRemaining = shieldAmount;
            if (enableShockwave) EmitShockwave();
        }

        /// <summary>
        /// 对象池复用后重新订阅事件。由 CardUnit.OnPoolSpawn 调用。
        /// OnPoolDespawn 会清除 OnAttackEvent/OnTakeDamageEvent/OnDestroyed，
        /// 因此需要在每次 Spawn 周期重新订阅。
        /// </summary>
        public void ResubscribeEvents()
        {
            if (_owner == null) return;
            _burstHitCounter = 0;
            if (enableSwarm || enableCharge || enableStunOnHit || enableSplash || enableBurstAttack)
                _owner.OnAttackEvent += OnAttack;
            if (enableStunOnHit || enableSplash || enableBurstAttack)
                _owner.OnPerTargetAttackEvent += OnPerTargetAttack;
            if (enableTear)
                _owner.OnAttackEvent += OnTearAttack;
            if (enableDeathExplosion || enableBurnOnDeath)
                _owner.OnDeathEvent += OnDeath;
            if (enableSummoner && summonOnKill)
                _owner.OnKillEvent += OnSummonerKill;
            // 对象池复用时重新注册盾墙（Awake 只执行一次，UnregisterShieldWall 在回池时注销）
            if (enableShieldWall && !_shieldWallUnits.Contains(this))
                _shieldWallUnits.Add(this);
            if (enableSniper)
            {
                _owner.OverrideFindTarget = FindSniperTarget;
                _owner.OverrideAttackRange = GetSniperAttackRange;
            }
            if (enableCavalryChase)
                _owner.OverrideFindTarget = FindCavalryChaseTarget;

            // 冲锋动画状态同步 + 重置冲锋状态（对象池复用时必须重新激活）
            if (enableCharge)
            {
                _isCharged = true;
                _originalSpeed = _owner.Stats.MoveSpeed;
                ApplyChargeSpeed(true);
                _owner.SetAnimBool("Charge", true);
                if (chargeHeightOverride != 0)
                    _owner.SetHeightOverride(chargeHeightOverride, chargeBlockableByHeight);
            }

            // 生成特效和音效
            _unitVFX?.PlaySpawn();
        }

        /// <summary>
        /// 对象池回收时注销盾墙缓存。由 CardUnit.OnPoolDespawn 调用。
        /// </summary>
        public void UnregisterShieldWall()
        {
            if (enableShieldWall)
                _shieldWallUnits.Remove(this);
        }

        private void OnDestroy()
        {
            // 盾墙单位从缓存注销
            if (enableShieldWall)
                _shieldWallUnits.Remove(this);

            if (_owner != null)
            {
                _owner.OnAttackEvent -= OnAttack;
                _owner.OnAttackEvent -= OnTearAttack;
                _owner.OnPerTargetAttackEvent -= OnPerTargetAttack;
                _owner.OnDeathEvent -= OnDeath;
                _owner.OnKillEvent -= OnSummonerKill;
                if (enableSniper || enableTaunt)
                    _owner.OverrideFindTarget = null;
                if (enableSniper)
                    _owner.OverrideAttackRange = null;
            }
        }

        public static float ApplyShieldWallGlobal(CardUnit damagedUnit, float damage, DamageType type)
        {
            float mult = 1f;
            Vector2 damagedPos = damagedUnit.VisualCenter;

            // 只遍历开启盾墙的单位（Awake 注册，OnDestroy 注销）
            for (int i = _shieldWallUnits.Count - 1; i >= 0; i--)
            {
                var sw = _shieldWallUnits[i];
                if (sw == null) { _shieldWallUnits.RemoveAt(i); continue; }
                var unit = sw._owner;
                if (unit == null || !unit.IsAlive || unit == damagedUnit) continue;
                if (unit.IsLandlord != damagedUnit.IsLandlord) continue;
                float dist = Vector2.Distance(damagedPos, unit.VisualCenter);
                if (dist <= sw.shieldRange)
                    mult *= (1f - sw.shieldDamageReduction);
            }

            return damage * mult;
        }

        private void OnAttack(CardUnit target)
        {
            _lastAttackTime = Time.time;  // 重置脱离战斗计时
            if (enableSwarm) ApplySwarm(target);
            if (enableCharge) ApplyCharge(target);
            // 单目标模式下 per-target 效果也在此触发
            if (_owner.MaxTargets <= 1)
            {
                if (enableStunOnHit && target != null)
                {
                    target.StunTimer = stunDuration;
                    _owner.TriggerAnim("StunHit");
                    _unitAudio?.PlayStunHit();
                    _unitVFX?.PlayStunHit(target.transform);
                }
                if (enableSplash) EmitSplash(target);
                if (enableBurstAttack) ApplyBurstAttack();
            }
        }

        /// <summary>多目标模式下每个目标独立触发的效果（溅射/眩晕/连击）</summary>
        private void OnPerTargetAttack(CardUnit target)
        {
            if (enableStunOnHit && target != null)
            {
                target.StunTimer = stunDuration;
                _owner.TriggerAnim("StunHit");
                _unitAudio?.PlayStunHit();
                _unitVFX?.PlayStunHit(target.transform);
            }
            if (enableSplash) EmitSplash(target);
            if (enableBurstAttack) ApplyBurstAttack();
        }

        private void ApplyBurstAttack()
        {
            _burstHitCounter++;
            if (_burstHitCounter >= burstHitCount)
            {
                _burstHitCounter = 0;
                _owner.StunTimer = burstCooldown;
            }
        }

        private void OnDeath()
        {
            if (enableDeathExplosion) EmitDeathExplosion();
            if (enableBurnOnDeath) EmitBurn();
        }

        private void Update()
        {
            // BOSS 未激活时跳过所有被动逻辑
            if (_owner._isBoss)
            {
                var bossCtrl = _owner.GetComponent<BossController>();
                if (bossCtrl != null && !bossCtrl.IsActive) return;
            }

            // 首帧同步冲锋动画状态（Awake 时 Animator 可能未就绪）
            if (enableCharge && !_chargeAnimSynced)
            {
                _owner.SetAnimBool("Charge", _isCharged);
                _chargeAnimSynced = true;
            }

            if (enableKingAura) UpdateKingAura();
            if (enableCharge) UpdateCharge();
            if (enableSummoner) UpdateSummoner();
            if (enableSlowAura) UpdateSlowAura();
        }

        private void OnDrawGizmos()
        {
            var owner = GetComponent<CardUnit>();
            Vector3 center = owner != null ? (Vector3)owner.VisualCenter : transform.position;
            if (enableSwarm) { Gizmos.color = new Color(0f, 1f, 1f, 0.3f); Gizmos.DrawWireSphere(center, swarmRadius); }
            if (enableKingAura) { Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f); Gizmos.DrawWireSphere(center, kingRadius); }
            if (enableShieldWall) { Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.3f); Gizmos.DrawWireSphere(center, shieldRange); }
            if (enableTaunt) { Gizmos.color = new Color(1f, 0f, 1f, 0.3f); Gizmos.DrawWireSphere(center, tauntRadius); }
            if (enableSlowAura) { Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f); Gizmos.DrawWireSphere(center, slowRadius); }
            if (enableShockwave) { Gizmos.color = new Color(1f, 0.2f, 0f, 0.3f); Gizmos.DrawWireSphere(center, shockwaveRadius); }
            if (enableBurnOnDeath) { Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); Gizmos.DrawWireSphere(center, burnRadius); }
            if (enableSplash) { Gizmos.color = new Color(1f, 0.6f, 0f, 0.3f); Gizmos.DrawWireSphere(center, splashRadius); }
            if (enableSniper)
            {
                float displayR = sniperRangeBonus > 0f ? sniperRangeBonus : (owner != null ? owner.DetectionRange : 5f);
                Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
                Gizmos.DrawWireSphere(center, displayR);
            }
            if (enableCavalryChase)
            {
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
                Gizmos.DrawWireSphere(center, Mathf.Max(owner != null ? owner.Stats.Range * 2f : 10f, 10f));
            }
        }

        // ══════════════════════════════════════════
        //  点杀
        // ══════════════════════════════════════════

        private CardUnit FindSniperTarget()
        {
            float normalRange = _owner.Stats.Range;
            // 点杀搜索范围：优先用 sniperRangeBonus，否则用索敌范围
            float sniperSearchRange = sniperRangeBonus > 0f ? sniperRangeBonus : _owner.DetectionRange;
            CardUnit bestIn = null, bestOut = null;
            float bestHpIn = float.MaxValue, bestHpOut = float.MaxValue;
            int count = Physics2D.OverlapCircle(_owner.VisualCenter, sniperSearchRange, _overlapFilter, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (enemy == null || !enemy.IsAlive || enemy.IsLandlord == _owner.IsLandlord) continue;
                if (!_owner.CanAttackHeight(enemy.UnitHeight)) continue;
                float dist = _owner.GetUnitEdgeDistance(enemy);
                if (dist > sniperSearchRange) continue;
                float hpRatio = enemy.CurrentHP / enemy.Stats.HP;
                if (dist <= normalRange) { if (hpRatio < bestHpIn) { bestHpIn = hpRatio; bestIn = enemy; } }
                else if (hpRatio <= sniperHpThreshold) { if (hpRatio < bestHpOut) { bestHpOut = hpRatio; bestOut = enemy; } }
            }
            _currentSniperTarget = bestIn ?? bestOut;
            return _currentSniperTarget;
        }

        /// <summary>点杀目标使用扩展攻击范围，其他目标使用默认攻击范围。</summary>
        private float GetSniperAttackRange(CardUnit target)
        {
            if (target == null) return _owner.Stats.Range;
            if (target == _currentSniperTarget && sniperRangeBonus > 0f)
            {
                float hpRatio = target.CurrentHP / target.Stats.HP;
                if (hpRatio <= sniperHpThreshold)
                    return sniperRangeBonus;
            }
            return _owner.Stats.Range;
        }

        // ══════════════════════════════════════════
        //  骑兵追远程
        // ══════════════════════════════════════════

        /// <summary>
        /// 骑兵索敌：优先锁定攻击距离 ≥ 阈值的敌方单位，按边缘距离排序。
        /// 若无远程目标，回退到默认最近索敌。
        /// </summary>
        private CardUnit FindCavalryChaseTarget()
        {
            float searchRange = Mathf.Max(_owner.Stats.Range * 2f, 5f);
            CardUnit bestRanged = null;
            float bestRangedDist = float.MaxValue;
            CardUnit bestAny = null;
            float bestAnyDist = float.MaxValue;

            // 使用缓存数组，避免分配
            int count = Physics2D.OverlapCircle(_owner.VisualCenter, searchRange, _overlapFilter, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (enemy == null || !enemy.IsAlive || enemy.IsLandlord == _owner.IsLandlord) continue;
                if (!_owner.CanAttackHeight(enemy.UnitHeight)) continue;

                float dist = Vector2.Distance(_owner.VisualCenter, enemy.VisualCenter);
                if (dist > searchRange) continue;

                // 优先：攻击距离 ≥ 阈值的远程单位
                if (enemy.Stats.Range >= cavalryChaseRangeThreshold)
                {
                    if (dist < bestRangedDist) { bestRangedDist = dist; bestRanged = enemy; }
                }
                // 备选：任意最近敌人
                if (dist < bestAnyDist) { bestAnyDist = dist; bestAny = enemy; }
            }

            return bestRanged ?? bestAny;
        }

        // ══════════════════════════════════════════
        //  君王光环
        // ══════════════════════════════════════════

        private void UpdateKingAura()
        {
            if (!_owner.IsAlive) return;
            _kingTimer += Time.deltaTime;
            if (_kingTimer >= kingInterval)
            {
                _kingTimer = 0f;
                if (!_owner.IsAttacking) _owner.TriggerAnim("KingAura");
                _unitAudio?.PlayKingAura();
                _unitVFX?.PlayKingAura(_owner.transform, kingRadius);
                int num = Physics2D.OverlapCircle(_owner.VisualCenter, kingRadius, _overlapFilter, _overlapBuffer);
                for (int i = 0; i < num; i++)
                {
                    var enemy = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                    if (enemy == null || !enemy.IsAlive || enemy._isBuilding || enemy.IsLandlord == _owner.IsLandlord) continue;
                    Vector2 pushDir = enemy.VisualCenter - _owner.VisualCenter;
                    if (pushDir.sqrMagnitude < 0.001f)
                        pushDir = Random.insideUnitCircle.normalized;
                    // v2.0: 仅 Master 端执行击退
                    if (_owner.SimulatesCombat)
                        StartCoroutine(KnockbackCoroutine(enemy, pushDir.normalized));
                }
            }
        }

        private IEnumerator KnockbackCoroutine(CardUnit target, Vector2 direction)
        {
            float duration = 0.2f;
            float elapsed = 0f;
            Vector3 startPos = target.transform.position;
            Vector3 endPos = startPos + (Vector3)(direction * kingPushDistance);

            while (elapsed < duration)
            {
                if (target == null || !target.IsAlive) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 缓动曲线：快起慢停
                float eased = 1f - (1f - t) * (1f - t);
                target.transform.position = Vector3.Lerp(startPos, endPos, eased);
                yield return null;
            }
        }

        // ══════════════════════════════════════════
        //  冲锋一击
        // ══════════════════════════════════════════

        private void UpdateCharge()
        {
            if (_isCharged) return;
            // 攻击中不重新进入冲锋状态，防止打断攻击动画
            if (_owner.IsAttacking) return;

            // 脱离战斗后才开始计时
            float timeSinceAttack = Time.time - _lastAttackTime;
            if (timeSinceAttack < chargeCooldown) return;

            EnterChargeState();
        }

        private void ApplyChargeSpeed(bool charged)
        {
            if (charged)
                _owner.ApplyBuff("charge", new CardUnit.StatBuff(moveSpeed: chargeSpeedMultiplier));
            else
                _owner.RemoveBuff("charge");
        }

        private void EnterChargeState()
        {
            _isCharged = true;
            ApplyChargeSpeed(true);
            _owner.SetAnimBool("Charge", true);
            if (chargeHeightOverride != 0)
                _owner.SetHeightOverride(chargeHeightOverride, chargeBlockableByHeight);
            _unitAudio?.PlayCharge();
        }

        private void ExitChargeState()
        {
            _isCharged = false;
            ApplyChargeSpeed(false);
            _owner.SetAnimBool("Charge", false);
            _owner.ClearHeightOverride();
        }

        private void ApplyCharge(CardUnit target)
        {
            if (!_isCharged) return;

            // 消耗冲锋状态
            ExitChargeState();

            // 冲锋伤害加成
            float chargeBonus = _owner.Stats.ATK * (chargeMultiplier - 1f);
            _owner._bonusDamage += chargeBonus;
            _unitVFX?.PlayCharge(_owner.transform);

            // 记录攻击时间（用于脱离战斗计时）
            _lastAttackTime = Time.time;
        }

        // ══════════════════════════════════════════
        //  人海连击
        // ══════════════════════════════════════════

        private void ApplySwarm(CardUnit target)
        {
            int count = 0;
            int num = Physics2D.OverlapCircle(_owner.VisualCenter, swarmRadius, _overlapFilter, _overlapBuffer);
            for (int i = 0; i < num; i++)
            {
                var ally = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (ally == null || !ally.IsAlive || ally._isBuilding || ally == _owner) continue;
                if (ally.IsLandlord != _owner.IsLandlord) continue;
                count++;
            }
            if (count > 0)
                _owner._bonusDamage += _owner.Stats.ATK * swarmDamagePct * count;
        }

        // ══════════════════════════════════════════
        //  盾墙线
        // ══════════════════════════════════════════
        //  减速光环
        // ══════════════════════════════════════════

        private void UpdateSlowAura()
        {
            if (!_owner.IsAlive) return;
            int num = Physics2D.OverlapCircle(_owner.VisualCenter, slowRadius, _overlapFilter, _overlapBuffer);
            for (int i = 0; i < num; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (enemy == null || !enemy.IsAlive || enemy._isBuilding || enemy.IsLandlord == _owner.IsLandlord) continue;
                // 应用减速 Buff（重复调用会覆盖同一 buffId）
                enemy.ApplyBuff("slow_aura", new CardUnit.StatBuff(moveSpeed: 1f - slowPercent));
                // 重置减速恢复计时器
                enemy.SlowRestoreTimer = slowDuration;
            }
        }

        // ══════════════════════════════════════════
        //  撕裂（易伤叠加）
        // ══════════════════════════════════════════

        private void OnTearAttack(CardUnit target)
        {
            if (target == null) return;
            target.TearStacks = Mathf.Min(target.TearStacks + 1, tearMaxStacks);
            target.TearTimer = tearDuration;
            target.TearDamagePerStack = tearDamagePerStack;
            _unitVFX?.PlayTear(target.transform);
        }

        private void UpdateTears()
        {
            if (_owner == null || !_owner.IsAlive) return;
            if (_owner.TearTimer > 0f)
            {
                _owner.TearTimer -= Time.deltaTime;
                if (_owner.TearTimer <= 0f)
                    _owner.TearStacks = 0;
            }
        }

        public static float GetTearMultiplier(CardUnit target)
        {
            if (target == null || target.TearStacks <= 0) return 1f;
            float perStack = target.TearDamagePerStack > 0f ? target.TearDamagePerStack : 0.05f;
            return 1f + target.TearStacks * perStack;
        }

        // ══════════════════════════════════════════
        //  出场震波
        // ══════════════════════════════════════════

        private void EmitShockwave()
        {
            _owner.TriggerAnim("Shockwave");
            _unitAudio?.PlayShockwave();
            _unitVFX?.PlayShockwave(_owner.VisualCenter, shockwaveRadius);
            int num = Physics2D.OverlapCircle(_owner.VisualCenter, shockwaveRadius, _overlapFilter, _overlapBuffer);
            for (int i = 0; i < num; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (enemy == null || !enemy.IsAlive || enemy.IsLandlord == _owner.IsLandlord) continue;
                if (!_owner.CanAttackHeight(enemy.UnitHeight)) continue;
                // B8: 安全防御——零向量时使用随机方向避免 NaN
                Vector2 pushDir = enemy.VisualCenter - _owner.VisualCenter;
                if (pushDir.sqrMagnitude < 0.001f)
                    pushDir = Random.insideUnitCircle.normalized;
                // v2.0: 仅 Master 端执行击退和伤害
                if (_owner.SimulatesCombat)
                {
                    enemy.transform.position += (Vector3)(pushDir.normalized);
                    enemy.TakeDamage(_owner.Stats.ATK * shockwaveDamagePct, DamageType.Physical);
                }
            }
        }

        // ══════════════════════════════════════════
        //  死亡爆炸
        // ══════════════════════════════════════════

        private void EmitDeathExplosion()
        {
            _owner.TriggerAnim("DeathExplosion");
            _unitAudio?.PlayDeathExplosion();
            _unitVFX?.PlayDeathExplosion(_owner.VisualCenter, explosionRadius);
            int num = Physics2D.OverlapCircle(_owner.VisualCenter, explosionRadius, _overlapFilter, _overlapBuffer);
            float damage = _owner.Stats.ATK * explosionDamagePct;
            for (int i = 0; i < num; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (enemy == null || !enemy.IsAlive || enemy.IsLandlord == _owner.IsLandlord) continue;
                if (!_owner.CanAttackHeight(enemy.UnitHeight)) continue;
                enemy.TakeDamage(damage, DamageType.Physical);
            }
        }

        // ══════════════════════════════════════════
        //  死亡燃烧
        // ══════════════════════════════════════════

        private void EmitBurn()
        {
            _owner.TriggerAnim("Burn");
            _unitAudio?.PlayBurn();
            _unitVFX?.PlayBurn(_owner.VisualCenter, burnRadius, burnDuration);
            var go = new GameObject("BurnZone");
            go.transform.position = _owner.VisualCenter;
            var zone = go.AddComponent<BurnZone>();
            zone.Init(_owner.IsLandlord, _owner.Stats.ATK * burnDamagePct, burnRadius, burnDuration, _owner.CanAttackHeight);
        }

        // ══════════════════════════════════════════
        //  溅射攻击
        // ══════════════════════════════════════════

        private void EmitSplash(CardUnit target)
        {
            // 目标无效时不发射溅射（防止溅射飞向无关建筑）
            if (target == null || !target.IsAlive) return;

            _owner.TriggerAnim("Splash");
            _unitAudio?.PlaySplash();

            // 从攻击者到目标碰撞箱的最近点作为溅射圆心，避免大型建筑溅射不到边缘单位
            Vector3 center = target.VisualCenter;
            if (target.TryGetComponent<Collider2D>(out var col))
                center = col.ClosestPoint(_owner.VisualCenter);

            // 播放溅射爆炸特效
            _unitVFX?.PlaySplash(center, splashRadius);

            // 排除主目标（避免双重伤害）
            CardUnit primaryTarget = target;

            int num = Physics2D.OverlapCircle(center, splashRadius, _overlapFilter, _overlapBuffer);
            float damage = _owner.Stats.ATK * splashDamagePct;
            for (int i = 0; i < num; i++)
            {
                var splashTarget = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (splashTarget == null || splashTarget == primaryTarget || !splashTarget.IsAlive) continue;
                if (splashTarget.IsLandlord == _owner.IsLandlord) continue;
                if (!_owner.CanAttackHeight(splashTarget.UnitHeight)) continue;
                splashTarget.TakeDamage(damage, DamageType.Physical);
            }
        }

        // 召唤师逻辑已拆分到 UnitPassives.Summon.cs
    }

    /// <summary>燃烧区域（独立类，供 UnitPassives 和 BattleManager 共用）</summary>
    public class BurnZone : MonoBehaviour
    {
        private bool _ownerIsLandlord;
        private float _dps, _radius, _lifetime;
        private System.Func<UnitHeight, bool> _canAttackHeight;
        // B10: 缓存数组避免每帧分配
        private readonly Collider2D[] _burnCache = new Collider2D[64];
        private float _burnTickTimer;
        private const float BurnTickInterval = 0.25f;

        public void Init(bool isLandlord, float dps, float radius, float duration, System.Func<UnitHeight, bool> canAttackHeight = null)
        {
            _ownerIsLandlord = isLandlord; _dps = dps; _radius = radius; _lifetime = duration;
            _canAttackHeight = canAttackHeight;
            Destroy(gameObject, duration);
        }
        private void Update()
        {
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0) return;
            // B10: 使用 Tick 间隔减少性能开销
            _burnTickTimer += Time.deltaTime;
            if (_burnTickTimer < BurnTickInterval) return;
            _burnTickTimer = 0f;

            float damagePerTick = _dps * BurnTickInterval;
            // 使用 ContactFilter2D + 缓存数组，避免每次分配新数组
            var filter = new ContactFilter2D().NoFilter();
            int count = Physics2D.OverlapCircle((Vector2)transform.position, _radius, filter, _burnCache);
            for (int i = 0; i < count; i++)
            {
                var enemy = _burnCache[i].GetComponentInParent<CardUnit>();
                if (enemy != null && enemy.IsAlive && enemy.IsLandlord != _ownerIsLandlord
                    && (_canAttackHeight == null || _canAttackHeight(enemy.UnitHeight)))
                    enemy.TakeDamage(damagePerTick, DamageType.Burn);
            }
        }
    }
}
