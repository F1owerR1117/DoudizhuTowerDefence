using System.Collections;
using System.Collections.Generic;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Gameplay.Battle;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>溅射形态</summary>
    public enum SplashMode
    {
        /// <summary>命中点圆形溅射（原始行为）</summary>
        AroundTarget = 0,
        /// <summary>朝向前方、且在自身攻击范围内的所有可攻击目标</summary>
        FacingSweep = 1,
    }

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

        [Header("人海")]
        [Tooltip("启用后按周围友军数量实时提升自身攻击力")]
        public bool enableSwarm;
        [Tooltip("统计友军的半径")]
        public float swarmRadius = 2f;
        [Tooltip("每名友军提升的攻击力百分比 (0.5 = +50%/人，无上限)")]
        public float swarmDamagePct = 0.5f;
        /// <summary>上一次统计到的友军数量（-1 = 未统计，用于避免每帧重复写 Buff）</summary>
        private int _swarmCount = -1;
        private const string SwarmBuffId = "swarm_atk";

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
        [Tooltip("冲锋期间可攻击的目标高度（留空=沿用单位自身的可攻击高度）")]
        public UnitHeight chargeAttackableHeight = 0;

        [Header("光环通用")]
        [Tooltip("勾选后，减速光环 / 君王光环（击退）只作用于本单位「可攻击到的高度」的目标，与伤害类被动口径一致；默认关闭（对范围内所有敌方单位生效）")]
        public bool auraOnlyAffectAttackable = false;

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
        [Tooltip("每累计多少次攻击触发一次眩晕（1 = 每次攻击都触发）")]
        public int stunEveryNHits = 1;

        [Header("流血")]
        [Tooltip("启用后攻击会为目标施加流血")]
        public bool enableBleed;
        [Tooltip("每层流血每秒伤害（占攻击力比例，0.3 = 30% ATK/秒）")]
        public float bleedDamagePctPerSec = 0.3f;
        [Tooltip("流血持续时间（秒），重复命中会刷新")]
        public float bleedDuration = 3f;
        [Tooltip("流血最大叠加层数")]
        public int bleedMaxStacks = 3;

        [Header("燃烧")]
        [Tooltip("启用后攻击会点燃目标")]
        public bool enableIgnite;
        [Tooltip("每层燃烧每秒伤害（占攻击力比例）")]
        public float igniteDamagePctPerSec = 0.2f;
        [Tooltip("燃烧持续时间（秒），重复命中会刷新")]
        public float igniteDuration = 4f;
        [Tooltip("燃烧最大叠加层数")]
        public int igniteMaxStacks = 5;
        [Tooltip("燃烧扩散半径：同阵营单位在此范围内停留足够久也会被点燃")]
        public float igniteSpreadRadius = 3f;
        [Tooltip("扩散所需的累计停留时间（秒），中途离开会清零")]
        public float igniteSpreadDwell = 1.5f;

        [Header("损伤")]
        [Tooltip("启用后攻击会为命中目标施加减速")]
        public bool enableImpair;
        [Tooltip("减速百分比 (0.3 = 移速×0.7)")]
        [Range(0f, 1f)]
        public float impairSlowPercent = 0.3f;
        [Tooltip("减速持续时间（秒）")]
        public float impairDuration = 2f;

        [Header("重击")]
        [Tooltip("启用后攻击会击退命中的敌人")]
        public bool enableHeavyStrike;
        [Tooltip("击退距离（格）")]
        public float heavyStrikePushDistance = 1.5f;

        [Header("过热")]
        [Tooltip("启用后持续攻击会不断提升自身攻击力")]
        public bool enableOverheat;
        [Tooltip("每持续攻击满 1 秒提升的攻击力比例（0.1 = +10%/秒）")]
        public float overheatAtkPctPerSec = 0.1f;
        [Tooltip("攻击力加成上限（比例，1.0 = 最多 +100%）")]
        public float overheatMaxBonusPct = 1f;
        [Tooltip("停止攻击后加成保留时间（秒），到期立即清零")]
        public float overheatRetainDuration = 3f;

        [Header("快速连击")]
        [Tooltip("启用后快速攻击 N 次后自我眩晕进入冷却")]
        public bool enableBurstAttack;
        [Tooltip("连击次数")]
        public int burstHitCount = 3;
        [Tooltip("连击期间攻击间隔倍率：1 = 不变；0.5 = 间隔减半（出手更快）；2 = 变慢")]
        [Range(0.1f, 3f)]
        public float burstAttackIntervalMult = 1f;
        [Tooltip("自我眩晕冷却时间（秒）")]
        public float burstCooldown = 2f;
        private int _burstHitCounter;
        private const string BurstAttackBuffId = "burst_attack";
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
        [Tooltip("击退距离（格）；0 = 只造成伤害、不击退")]
        public float shockwavePushDistance = 1f;
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

        [Header("群体攻击")]
        [Tooltip("启用后攻击时对目标周围敌方单位造成范围伤害")]
        public bool enableSplash;
        [Tooltip("溅射形态：AroundTarget=命中点圆形（默认）；FacingSweep=朝向前方、且在自身攻击范围内的所有可攻击目标")]
        public SplashMode splashMode = SplashMode.AroundTarget;
        [Tooltip("溅射半径（仅 AroundTarget 形态使用）")]
        public float splashRadius = 2f;
        [Tooltip("溅射伤害百分比 (0.5 = 50% ATK)")]
        public float splashDamagePct = 0.5f;
        [Tooltip("方向型溅射的扇形角度：180=面前半平面，90=正前方90°扇形，360=全向")]
        public float splashFacingAngle = 180f;
        [Tooltip("扇形相对朝向的旋转偏移（度）。0=正对前方；正值逆时针（朝上），负值顺时针（朝下）")]
        public float splashFacingRotation = 0f;
        [Tooltip("主目标是否也受溅射伤害（默认关闭，避免与普攻叠加）")]
        public bool splashHitsPrimary = false;

        [Header("处决")]
        [Tooltip("启用后：本次伤害真实扣血后目标血量低于阈值，则直接死亡（伤害全被护盾吸收时不触发）")]
        public bool enableExecute;
        [Tooltip("处决阈值（绝对血量）：结算后目标血量严格低于该值时被处决")]
        public float executeThreshold = 50f;

        [Header("治疗")]
        [Tooltip("启用后把受伤友方作为攻击目标进行治疗（走完整攻击流程）")]
        public bool enableHeal;
        [Tooltip("每次治疗量（HP）")]
        public float healAmount = 30f;
        [Tooltip("已废弃：治疗频率跟随攻击间隔（AttackInterval），此字段不再生效")]
        public float healInterval = 1.5f;
        [Tooltip("治疗半径；0 = 使用自身攻击距离")]
        public float healRadius = 0f;
        [Tooltip("允许被治疗的目标高度（空中/地面）")]
        public UnitHeight healTargetHeight = UnitHeight.Ground | UnitHeight.Air;
        [Tooltip("优先治疗血量百分比最低的友方；关闭则治疗距离最近的友方")]
        public bool healLowestHpFirst = true;

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
        [Tooltip("每次定时召唤生成的数量（受同时存活上限限制；击杀召唤固定为 1）")]
        public int summonCountPerCast = 1;
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
        private float _summonChannelTimer;
        private float _summonChannelLimit;
        private float _kingTimer;

        // 击晕计数（按攻击次数）
        private int _stunHitCounter;
        private bool _stunThisAttack;

        // 过热
        private float _overheatLastAttackTime = -999f;
        private float _overheatTime;
        private float _overheatAppliedBonus = -1f;
        private const string OverheatBuffId = "overheat_atk";

        /// <summary>是否存在需要在命中时结算的被动（用于事件订阅判断）</summary>
        private bool HasOnHitPassives =>
            enableStunOnHit || enableSplash || enableBurstAttack ||
            enableBleed || enableIgnite || enableImpair || enableHeavyStrike;
        private CardUnit _healTarget;
        private float _healScanTimer;
        private const float HealScanInterval = 0.15f;
        private CardUnit _currentSniperTarget;
        private CardUnit _lastSniperTarget;

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
            if (enableCharge || HasOnHitPassives) _owner.OnAttackEvent += OnAttack;
            if (HasOnHitPassives) _owner.OnPerTargetAttackEvent += OnPerTargetAttack;
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
                if (chargeAttackableHeight != 0)
                    _owner.SetAttackableHeightOverride(chargeAttackableHeight);
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
            if (enableShieldAbsorb)
            {
                _owner.DamageAbsorbRemaining = shieldAmount;
                _unitAudio?.PlayShieldAbsorb();
                _unitVFX?.SpawnShield(_owner.transform);
            }
            if (enableSlowAura)
            {
                _unitAudio?.PlaySlowAura();
                _unitVFX?.SetSlowAura(true, slowRadius);
            }
            if (enableLeader)
            {
                _unitAudio?.PlayLeader();
                _unitVFX?.SetLeaderAura(true, leaderRadius);
            }
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
            _swarmCount = -1;
            _owner.RemoveBuff(SwarmBuffId);
            _stunHitCounter = 0;
            _stunThisAttack = false;
            _overheatTime = 0f;
            _overheatAppliedBonus = -1f;
            _overheatLastAttackTime = -999f;
            _owner.RemoveBuff(OverheatBuffId);
            if (enableCharge || HasOnHitPassives)
                _owner.OnAttackEvent += OnAttack;
            if (HasOnHitPassives)
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
                if (chargeAttackableHeight != 0)
                    _owner.SetAttackableHeightOverride(chargeAttackableHeight);
            }

            // 生成特效和音效
            _unitVFX?.PlaySpawn();

            // 每轮 Spawn 周期重新建立领袖光环
            if (enableLeader)
            {
                ClearLeaderBuffs();
                _leaderScanTimer = 0f;
                _unitAudio?.PlayLeader();
                _unitVFX?.SetLeaderAura(true, leaderRadius);
            }
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

            // 领袖死亡/销毁：移除所有友军身上的加成
            ClearLeaderBuffs();

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
            // 盾墙只保护单位，不保护建筑
            if (damagedUnit == null || damagedUnit._isBuilding) return damage;

            float mult = 1f;

            // 只遍历开启盾墙的单位（Awake 注册，OnDestroy 注销）
            for (int i = _shieldWallUnits.Count - 1; i >= 0; i--)
            {
                var sw = _shieldWallUnits[i];
                if (sw == null) { _shieldWallUnits.RemoveAt(i); continue; }
                var unit = sw._owner;
                if (unit == null || !unit.IsAlive || unit == damagedUnit) continue;
                if (unit.IsLandlord != damagedUnit.IsLandlord) continue;
                // 统一口径：与治疗/攻击范围一致，用边缘距离（碰撞体间隙）
                if (damagedUnit.GetUnitEdgeDistance(unit) <= sw.shieldRange)
                    mult *= (1f - sw.shieldDamageReduction);
            }

            return damage * mult;
        }

        private void OnAttack(CardUnit target)
        {
            _lastAttackTime = Time.time;  // 重置脱离战斗计时
            _overheatLastAttackTime = Time.time;
            if (enableCharge) ApplyCharge(target);

            // 方向型溅射：每次攻击只触发一次（多目标模式下也不随目标数翻倍）
            if (enableSplash && splashMode == SplashMode.FacingSweep)
                EmitFacingSweep(target);

            // 击晕计数：按「攻击次数」累计（多目标一次攻击只算 1 次）
            _stunThisAttack = false;
            if (enableStunOnHit)
            {
                _stunHitCounter++;
                int need = Mathf.Max(1, stunEveryNHits);
                if (_stunHitCounter >= need)
                {
                    _stunHitCounter = 0;
                    _stunThisAttack = true;
                }
            }

            // 单目标模式下 per-target 效果也在此触发
            if (_owner.MaxTargets <= 1)
            {
                ApplyOnHitEffects(target);
                if (enableSplash && splashMode == SplashMode.AroundTarget) EmitSplash(target);
                if (enableBurstAttack) ApplyBurstAttack();
            }
        }

        /// <summary>多目标模式下每个目标独立触发的效果（溅射/眩晕/连击）</summary>
        private void OnPerTargetAttack(CardUnit target)
        {
            ApplyOnHitEffects(target);
            // 方向型溅射已在 OnAttack 中统一触发，这里只处理圆形溅射
            if (enableSplash && splashMode == SplashMode.AroundTarget) EmitSplash(target);
            if (enableBurstAttack) ApplyBurstAttack();
        }

        /// <summary>
        /// 命中单个目标时的效果入口。
        /// 节拍未启用 → 走原有全局被动；节拍启用 → 默认由节拍接管（可用 legacyOnHitPassives = Add 叠加）。
        /// 单目标与多目标共用同一入口，保证两种模式行为一致。
        /// </summary>
        private void ApplyOnHitEffects(CardUnit target)
        {
            if (target == null || !target.IsAlive) return;

            bool rhythmOn = RhythmActive;

            if (!rhythmOn || legacyOnHitPassives == LegacyOnHitMode.Add)
                ApplyLegacyOnHitEffects(target);

            if (rhythmOn)
                ApplyRhythmEffects(target);
        }

        /// <summary>
        /// 原有全局命中被动集合：眩晕 / 流血 / 燃烧 / 损伤 / 重击。
        /// </summary>
        private void ApplyLegacyOnHitEffects(CardUnit target)
        {
            // 眩晕（受「每 N 次攻击」限制）
            if (enableStunOnHit && _stunThisAttack)
            {
                target.StunTimer = stunDuration;
                _owner.TriggerAnim("StunHit");
                _unitAudio?.PlayStunHit();
                _unitVFX?.PlayStunHit(target.transform);
            }

            // 流血（按来源独立叠层）
            if (enableBleed)
            {
                target.ApplyBleed(_owner, _owner.Stats.ATK * bleedDamagePctPerSec, bleedDuration, bleedMaxStacks);
                _unitVFX?.PlayBleed(target.transform);
                _unitAudio?.PlayBleed();
            }

            // 燃烧（按兵种来源独立叠层）
            if (enableIgnite)
            {
                target.ApplyIgnite(_owner, _owner.Stats.ATK * igniteDamagePctPerSec, igniteDuration, igniteMaxStacks,
                                   igniteSpreadRadius, igniteSpreadDwell);
                _unitVFX?.PlayIgnite(target.transform);
                _unitAudio?.PlayIgnite();
            }

            // 损伤（减速）
            if (enableImpair)
            {
                target.ApplyImpair(impairSlowPercent, impairDuration);
                _unitVFX?.PlayImpair(target.transform);
                _unitAudio?.PlayImpair();
            }

            // 重击（击退 + 打断）
            if (enableHeavyStrike && heavyStrikePushDistance > 0f)
            {
                Vector2 dir = (Vector2)target.VisualCenter - (Vector2)_owner.VisualCenter;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                target.InterruptAttack();
                _unitVFX?.PlayHeavyStrike(target.VisualCenter, heavyStrikePushDistance);
                _unitAudio?.PlayHeavyStrike();
                StartCoroutine(KnockbackCoroutine(target, dir.normalized, heavyStrikePushDistance));
            }
        }

        private void ApplyBurstAttack()
        {
            _burstHitCounter++;
            if (_burstHitCounter >= burstHitCount)
            {
                _burstHitCounter = 0;
                _owner.StunTimer = burstCooldown;
                // 连击结束 → 恢复原攻击间隔
                _owner.RemoveBuff(BurstAttackBuffId);
                _unitVFX?.PlayBurst(_owner.transform);
                _unitAudio?.PlayBurst();
                return;
            }

            // 连击窗口内：按倍率调整攻击间隔（<1 更快）
            if (!Mathf.Approximately(burstAttackIntervalMult, 1f))
                _owner.ApplyBuff(BurstAttackBuffId, new CardUnit.StatBuff(atkInterval: burstAttackIntervalMult));
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
            if (enableSwarm) UpdateSwarm();
            if (enableOverheat) UpdateOverheat();
            if (enableSummoner) UpdateSummoner();
            if (enableSlowAura) UpdateSlowAura();
            if (enableLeader) UpdateLeaderAura();
        }

        private void OnDrawGizmos()
        {
            if (!GizmoDraw.ShouldDraw(gameObject)) return;

            var owner = GetComponent<CardUnit>();
            Vector3 center = owner != null ? (Vector3)owner.PreviewCenter : transform.position;
            Vector2 size = owner != null ? owner.PreviewColliderSize : new Vector2(1f, 1f);
            float range = owner != null ? owner.RangeForPreview : 5f;

            // ── OverlapCircle 口径：以自身中心为圆心的圆 ──
            if (enableSwarm) { GizmoDraw.Circle(center, swarmRadius, new Color(0f, 1f, 1f, 0.3f)); }
            if (enableKingAura) { GizmoDraw.Circle(center, kingRadius, new Color(1f, 0.8f, 0f, 0.3f)); }
            if (enableSlowAura) { GizmoDraw.Circle(center, slowRadius, new Color(0f, 0.5f, 1f, 0.3f)); }
            if (enableShockwave) { GizmoDraw.Circle(center, shockwaveRadius, new Color(1f, 0.2f, 0f, 0.3f)); }
            if (enableBurnOnDeath) { GizmoDraw.Circle(center, burnRadius, new Color(1f, 0.5f, 0f, 0.3f)); }

            // ── 边缘距离口径：碰撞体外膨胀（与运行时 GetUnitEdgeDistance 判定一致） ──
            if (enableShieldWall)
                GizmoDraw.ExpandedBounds(center, size, shieldRange, new Color(0.2f, 0.5f, 1f, 0.5f));
            if (enableTaunt)
                GizmoDraw.ExpandedBounds(center, size, tauntRadius, new Color(1f, 0f, 1f, 0.45f));
            if (enableHeal)
            {
                float hr = healRadius > 0f ? healRadius : range;
                GizmoDraw.ExpandedBounds(center, size, hr, new Color(0.35f, 1f, 0.45f, 0.7f));
            }

            // ── 飞扑：射程外 ~ pounceRange 的环形区间（边缘距离口径，内外圈按自身外扩量近似） ──
            if (enablePounce)
            {
                float outer = Mathf.Max(pounceRange, range);
                GizmoDraw.ExpandedBounds(center, size, outer, new Color(0.6f, 0.4f, 1f, 0.45f));
                GizmoDraw.ExpandedBounds(center, size, range, new Color(0.6f, 0.4f, 1f, 0.75f));
#if UNITY_EDITOR
                UnityEditor.Handles.Label(center + Vector3.up * (outer * 0.5f + 0.6f),
                    string.Format("飞扑区间 {0:0.#} ~ {1:0.#}m", range, outer), EditorLabelStyle());
#endif
            }

            // ── 群体攻击 ──
            if (enableSplash)
            {
                if (splashMode == SplashMode.FacingSweep)
                {
                    // 扇形预览：编辑期与运行期都可见（RangeForPreview 在未 Initialize 时回退到预制体 _range）
                    Vector3 dir = (owner != null && !owner.FacingRight) ? Vector3.left : Vector3.right;
                    dir = Rotate(dir, splashFacingRotation);      // 与运行时保持一致的旋转偏移
                    float angle = Mathf.Clamp(splashFacingAngle, 1f, 360f);
                    float half = angle * 0.5f;

#if UNITY_EDITOR
                    // 半透明实体扇形，便于肉眼判断覆盖范围
                    UnityEditor.Handles.color = new Color(1f, 0.45f, 0.12f, 0.16f);
                    UnityEditor.Handles.DrawSolidArc(center, Vector3.forward, Rotate(dir, -half), angle, range);
                    UnityEditor.Handles.Label(center + dir * range + Vector3.up * 0.35f,
                        string.Format("正面群体攻击 {0}° / {1:0.#}m{2}", angle, range,
                            Mathf.Approximately(splashFacingRotation, 0f) ? "" : string.Format(" / 偏转 {0:0}°", splashFacingRotation)),
                        EditorLabelStyle());
#endif
                    // 描边：弧 + 两条边线
                    GizmoDraw.Fan(center, dir, angle, range, new Color(1f, 0.45f, 0.12f, 0.85f));
                    // 朝向中线
                    Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.55f);
                    Gizmos.DrawLine(center, center + dir * range);
                }
                else
                {
                    // 圆形溅射的圆心是「命中点」，这里只示意半径
                    GizmoDraw.Circle(center, splashRadius, new Color(1f, 0.6f, 0f, 0.3f));
                }
            }

            // ── 点杀：搜索半径（运行时为 OverlapCircle + 边缘过滤） ──
            if (enableSniper)
            {
                float searchR = sniperRangeBonus > 0f
                    ? sniperRangeBonus
                    : (owner != null ? owner.DetectionRangeForPreview : range);
                GizmoDraw.ExpandedBounds(center, size, searchR, new Color(1f, 0f, 0f, 0.35f));
            }

            // ── 骑兵追远程：与运行时一致 Mathf.Max(射程×2, 5) ──
            if (enableCavalryChase)
            {
                float searchR = Mathf.Max(range * 2f, 5f);
                GizmoDraw.ExpandedBounds(center, size, searchR, new Color(0f, 1f, 0.5f, 0.35f));
            }

            // ── 光环高度过滤提示 ──
            if (auraOnlyAffectAttackable && (enableKingAura || enableSlowAura))
            {
#if UNITY_EDITOR
                float labelR = Mathf.Max(enableKingAura ? kingRadius : 0f, enableSlowAura ? slowRadius : 0f);
                UnityEditor.Handles.Label(center + Vector3.up * (labelR + 0.5f),
                    "光环：仅可攻击高度", EditorLabelStyle());
#endif
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

            // 锁定目标变化时播放标记
            if (_currentSniperTarget != _lastSniperTarget)
            {
                _lastSniperTarget = _currentSniperTarget;
                if (_currentSniperTarget != null)
                    _unitVFX?.PlaySniperMark(_currentSniperTarget.transform);
            }
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

            // 使用缓存数组，避免分配；预筛半径补上自身碰撞体半径，保证边缘距离口径不漏检
            float searchRadius = searchRange + GetColliderRadius(_owner);
            int count = Physics2D.OverlapCircle(_owner.VisualCenter, searchRadius, _overlapFilter, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (enemy == null || !enemy.IsAlive || enemy.IsLandlord == _owner.IsLandlord) continue;
                if (!_owner.CanAttackHeight(enemy.UnitHeight)) continue;

                // 统一口径：与其他索敌/攻击范围一致，用边缘距离
                float dist = _owner.GetUnitEdgeDistance(enemy);
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
                    // 光环通用开关：只影响本单位可攻击到的目标高度
                    if (auraOnlyAffectAttackable && !_owner.CanAttackHeight(enemy.UnitHeight)) continue;
                    Vector2 pushDir = enemy.VisualCenter - _owner.VisualCenter;
                    if (pushDir.sqrMagnitude < 0.001f)
                        pushDir = Random.insideUnitCircle.normalized;
                    // v2.0: 仅 Master 端执行击退
                    if (_owner.SimulatesCombat)
                        StartCoroutine(KnockbackCoroutine(enemy, pushDir.normalized, kingPushDistance));
                }
            }
        }

        // ══════════════════════════════════════════
        //  治疗
        // ══════════════════════════════════════════

        /// <summary>当前的待治疗目标（供 AI 转向使用）</summary>
        public CardUnit CurrentHealTarget => _healTarget;

        /// <summary>
        /// AI 调用：返回当前可治疗的友军（含自身），没有则返回 null。
        /// 治疗的「施放」走正常攻击流程（走位 / 攻击动画 / 攻击间隔），
        /// 攻击帧到达时由 ExecuteHeal 结算治疗而不是伤害。
        /// </summary>
        public CardUnit AcquireHealTarget()
        {
            if (!enableHeal || _owner == null || !_owner.IsAlive) return null;
            if (!_owner.SimulatesCombat) return null;

            // 目标扫描限频，避免每帧做一次圆形检索
            _healScanTimer += Time.deltaTime;
            bool needRescan = _healScanTimer >= HealScanInterval
                              || _healTarget == null
                              || !_healTarget.IsAlive
                              || _healTarget.HPRatio >= 0.999f;
            if (needRescan)
            {
                _healScanTimer = 0f;
                _healTarget = FindHealTarget();
            }
            return _healTarget;
        }

        /// <summary>攻击帧结算：对单个目标施加治疗（替代伤害）。</summary>
        public void ExecuteHealTarget(CardUnit target)
        {
            if (!enableHeal || target == null || !target.IsAlive) return;
            target.Heal(healAmount);
            _unitVFX?.PlayHeal(target.transform);
            _unitAudio?.PlayHeal();
        }

        /// <summary>
        /// 攻击帧结算：对一组目标施加治疗（多目标单位一次治疗全部选中目标）。
        /// 目标为空或已满血时跳过。
        /// </summary>
        public void ExecuteHealTargets(System.Collections.Generic.List<CardUnit> targets)
        {
            if (!enableHeal || targets == null) return;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || !t.IsAlive) continue;
                ExecuteHealTarget(t);
            }
        }

        /// <summary>
        /// 收集范围内所有可治疗目标（含自身），按「血量最低优先」或「距离最近优先」排序。
        /// 供多目标单位在攻击帧使用。
        /// </summary>
        public int CollectHealTargets(System.Collections.Generic.List<CardUnit> result, int max)
        {
            if (result == null) return 0;
            result.Clear();
            if (!enableHeal || _owner == null || !_owner.IsAlive) return 0;

            float radius = healRadius > 0f ? healRadius : _owner.Stats.Range;
            int num = Physics2D.OverlapCircle(_owner.VisualCenter, radius, _overlapFilter, _overlapBuffer);
            var found = new System.Collections.Generic.List<CardUnit>(num);
            for (int i = 0; i < num; i++)
            {
                var ally = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (ally == null || !ally.IsAlive) continue;
                if (ally != _owner && ally.IsLandlord != _owner.IsLandlord) continue;   // 只治友方（含自己）
                if ((healTargetHeight & ally.UnitHeight) == 0) continue;                // 高度不可治
                if (ally.HPRatio >= 0.999f) continue;                                   // 满血不治
                if (_owner.GetUnitEdgeDistance(ally) > radius) continue;                // 必须在治疗范围内
                if (found.Contains(ally)) continue;
                found.Add(ally);
            }

            if (healLowestHpFirst)
                found.Sort((a, b) => a.HPRatio.CompareTo(b.HPRatio));
            else
                found.Sort((a, b) => _owner.GetUnitEdgeDistance(a).CompareTo(_owner.GetUnitEdgeDistance(b)));

            int take = max > 0 ? Mathf.Min(max, found.Count) : found.Count;
            for (int i = 0; i < take; i++) result.Add(found[i]);
            return result.Count;
        }

        /// <summary>在治疗范围内挑选友方（含自身），按血量百分比或距离择优</summary>
        private CardUnit FindHealTarget()
        {
            float radius = healRadius > 0f ? healRadius : _owner.Stats.Range;
            Vector2 center = _owner.VisualCenter;

            int num = Physics2D.OverlapCircle(center, radius, _overlapFilter, _overlapBuffer);

            CardUnit best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < num; i++)
            {
                var ally = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (ally == null || !ally.IsAlive) continue;
                if (ally != _owner && ally.IsLandlord != _owner.IsLandlord) continue;   // 只治友方（含自己）
                if ((healTargetHeight & ally.UnitHeight) == 0) continue;                // 高度不可治
                if (ally.HPRatio >= 0.999f) continue;                                   // 满血不治
                if (_owner.GetUnitEdgeDistance(ally) > radius) continue;                // 必须在治疗范围内

                float score = healLowestHpFirst ? ally.HPRatio : _owner.GetUnitEdgeDistance(ally);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = ally;
                }
            }
            return best;
        }

        private IEnumerator KnockbackCoroutine(CardUnit target, Vector2 direction, float distance)
        {
            float duration = 0.2f;
            float elapsed = 0f;
            Vector3 startPos = target.transform.position;
            Vector3 endPos = startPos + (Vector3)(direction * distance);

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
            if (chargeAttackableHeight != 0)
                _owner.SetAttackableHeightOverride(chargeAttackableHeight);
            _unitAudio?.PlayCharge();
        }

        private void ExitChargeState()
        {
            _isCharged = false;
            ApplyChargeSpeed(false);
            _owner.SetAnimBool("Charge", false);
            _owner.ClearHeightOverride();
            _owner.ClearAttackableHeightOverride();
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
            // 人海已改为「持续性攻击力加成」，见 UpdateSwarm()
        }

        /// <summary>
        /// 过热：持续攻击时按累计时间提升攻击力；停止攻击后保留一段时间，到期立即清零。
        /// </summary>
        private void UpdateOverheat()
        {
            if (!enableOverheat || _owner == null) return;

            float interval = Mathf.Max(0.05f, _owner.Stats.AttackInterval);
            float sinceAttack = Time.time - _overheatLastAttackTime;
            bool attacking = sinceAttack <= interval * 1.5f;   // 两次攻击间隔足够近 → 视为连续攻击

            if (attacking)
            {
                _overheatTime += Time.deltaTime;
            }
            else if (sinceAttack > interval * 1.5f + Mathf.Max(0f, overheatRetainDuration))
            {
                _overheatTime = 0f;   // 保留时间结束 → 立即清零
            }

            int seconds = Mathf.FloorToInt(_overheatTime);
            float bonus = Mathf.Min(seconds * overheatAtkPctPerSec, overheatMaxBonusPct);
            if (bonus < 0f) bonus = 0f;

            if (Mathf.Approximately(bonus, _overheatAppliedBonus)) return;
            float previousBonus = _overheatAppliedBonus;
            _overheatAppliedBonus = bonus;

            if (bonus > 0.0001f)
                _owner.ApplyBuff(OverheatBuffId, new CardUnit.StatBuff(atk: 1f + bonus));
            else
                _owner.RemoveBuff(OverheatBuffId);

            // 特效/音效：进入过热时开启光环与提示音，清零时关闭
            _unitVFX?.SetOverheatAura(bonus > 0.0001f);
            if (bonus > 0.0001f && previousBonus <= 0f)
                _unitAudio?.PlayOverheat();
        }

        /// <summary>
        /// 人海：每帧统计半径内友军数量，按数量实时提升自身攻击力。
        /// 人数变化时才写入 Buff，避免每帧重复重算属性。
        /// </summary>
        private void UpdateSwarm()
        {
            if (!_owner.IsAlive)
            {
                if (_swarmCount != 0) { _swarmCount = 0; _owner.RemoveBuff(SwarmBuffId); }
                return;
            }

            int count = 0;
            int num = Physics2D.OverlapCircle(_owner.VisualCenter, swarmRadius, _overlapFilter, _overlapBuffer);
            for (int i = 0; i < num; i++)
            {
                var ally = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (ally == null || !ally.IsAlive || ally._isBuilding || ally == _owner) continue;
                if (ally.IsLandlord != _owner.IsLandlord) continue;
                count++;
            }

            if (count == _swarmCount) return;   // 人数没变，不重复写 Buff
            _swarmCount = count;

            if (count > 0)
                _owner.ApplyBuff(SwarmBuffId, new CardUnit.StatBuff(atk: 1f + swarmDamagePct * count));
            else
                _owner.RemoveBuff(SwarmBuffId);

            // 有友军时显示人海光环
            _unitVFX?.SetSwarmAura(count > 0, swarmRadius);
        }

        // ══════════════════════════════════════════
        //  盾墙线
        // ══════════════════════════════════════════
        //  减速光环
        // ══════════════════════════════════════════

        private void UpdateSlowAura()
        {
            if (!_owner.IsAlive) return;
            float mult = 1f - slowPercent;   // 越小 = 减速越强
            int num = Physics2D.OverlapCircle(_owner.VisualCenter, slowRadius, _overlapFilter, _overlapBuffer);
            for (int i = 0; i < num; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (enemy == null || !enemy.IsAlive || enemy._isBuilding || enemy.IsLandlord == _owner.IsLandlord) continue;
                // 光环通用开关：只影响本单位可攻击到的目标高度
                if (auraOnlyAffectAttackable && !_owner.CanAttackHeight(enemy.UnitHeight)) continue;

                bool alreadySlowed = enemy.SlowRestoreTimer > 0f;
                if (alreadySlowed)
                {
                    // 已有更强的减速 → 让更强的那个生效，本光环不覆盖
                    if (mult > enemy.SlowAuraMoveMult + 0.0001f) continue;
                    // 相同强度 → 只续时，不重复写入
                    if (Mathf.Abs(mult - enemy.SlowAuraMoveMult) <= 0.0001f)
                    {
                        enemy.SlowRestoreTimer = slowDuration;
                        continue;
                    }
                }

                // 未减速，或本次更强 → 覆盖为更强的减速
                enemy.SlowAuraMoveMult = mult;
                enemy.SlowRestoreTimer = slowDuration;
                enemy.RefreshSlowBuff();   // 与「损伤」通道取更强后统一生效
            }
        }

        // ══════════════════════════════════════════
        //  撕裂（易伤叠加）
        // ══════════════════════════════════════════

        private void OnTearAttack(CardUnit target)
        {
            if (target == null) return;
            // 按兵种来源独立叠层
            target.ApplyTear(_owner, tearDamagePerStack, tearDuration, tearMaxStacks);
            _unitVFX?.PlayTear(target.transform);
            _unitAudio?.PlayTear();
        }

        /// <summary>撕裂易伤倍率（各兵种来源的层数×每层% 相加）</summary>
        public static float GetTearMultiplier(CardUnit target)
        {
            if (target == null) return 1f;
            return target.TearMultiplier;
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
                    // 击退：按配置距离平滑推开（0 = 不击退）
                    if (shockwavePushDistance > 0f)
                        StartCoroutine(KnockbackCoroutine(enemy, pushDir.normalized, shockwavePushDistance));
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
                if (splashTarget == null || !splashTarget.IsAlive) continue;
                if (splashTarget == _owner) continue;
                if (splashTarget == primaryTarget && !splashHitsPrimary) continue;
                if (splashTarget.IsLandlord == _owner.IsLandlord) continue;
                if (!_owner.CanAttackHeight(splashTarget.UnitHeight)) continue;
                splashTarget.TakeDamage(damage, DamageType.Physical, _owner.Summoner != null ? _owner.Summoner : _owner);
            }
        }

        // ══════════════════════════════════════════
        //  方向型溅射（朝向前方 + 攻击范围内）
        // ══════════════════════════════════════════

        /// <summary>
        /// 对「朝向侧、且在自身攻击范围内」的所有可攻击目标造成一次溅射伤害。
        /// 判定标准与索敌一致：GetUnitEdgeDistance(t) &lt;= Stats.Range。
        /// 每次攻击只触发一次，不随多目标数量翻倍。
        /// </summary>
        private void EmitFacingSweep(CardUnit primaryTarget)
        {
            if (_owner == null) return;

            _owner.TriggerAnim("Splash");
            _unitAudio?.PlaySplash();

            Vector2 origin = _owner.VisualCenter;
            float range = Mathf.Max(0.1f, _owner.Stats.Range);
            Vector2 dir = _owner.FacingRight ? Vector2.right : Vector2.left;
            // 扇形中心方向 = 朝向 旋转 splashFacingRotation（0 时与朝向一致）
            if (!Mathf.Approximately(splashFacingRotation, 0f))
                dir = ((Vector2)Rotate(dir, splashFacingRotation)).normalized;

            // 特效放在身前，避免叠在攻击者身上
            if (_unitVFX != null)
            {
                float fxDist = Mathf.Clamp(range * 0.5f, 0.5f, 6f);
                _unitVFX.PlaySplash((Vector3)(origin + dir * fxDist), Mathf.Clamp(range * 0.6f, 1f, 6f));
            }

            float halfAngle = Mathf.Clamp(splashFacingAngle, 1f, 360f) * 0.5f;
            // 搜索半径：攻击范围 + 自身碰撞体半径，保证边缘距离判定不会漏掉大型目标
            float searchRadius = range + GetColliderRadius(_owner);

            int num = Physics2D.OverlapCircle(origin, searchRadius, _overlapFilter, _overlapBuffer);
            float damage = _owner.Stats.ATK * splashDamagePct;

            for (int i = 0; i < num; i++)
            {
                var t = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (t == null || t == _owner || !t.IsAlive) continue;
                if (t == primaryTarget && !splashHitsPrimary) continue;
                if (t.IsLandlord == _owner.IsLandlord) continue;
                if (!_owner.CanAttackHeight(t.UnitHeight)) continue;
                if (_owner.GetUnitEdgeDistance(t) > range) continue;

                Vector2 to = t.VisualCenter - origin;
                if (to.sqrMagnitude < 0.0001f) continue;
                if (Vector2.Angle(dir, to) > halfAngle) continue;

                t.LastAttacker = _owner.Summoner != null ? _owner.Summoner : _owner;
                t.TakeDamage(damage, DamageType.Physical, _owner.Summoner != null ? _owner.Summoner : _owner);
            }
        }

        private static float GetColliderRadius(CardUnit unit)
        {
            var col = unit.GetComponent<Collider2D>();
            return col != null ? col.bounds.extents.magnitude : 1f;
        }

        /// <summary>把方向向量绕 Z 轴旋转指定角度（Gizmos 用）</summary>
        private static Vector3 Rotate(Vector3 dir, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector3(dir.x * c - dir.y * s, dir.x * s + dir.y * c, 0f);
        }

#if UNITY_EDITOR
        private static GUIStyle _labelStyle;
        private static GUIStyle EditorLabelStyle()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
                _labelStyle.normal.textColor = new Color(1f, 0.6f, 0.25f);
                _labelStyle.fontSize = 11;
            }
            return _labelStyle;
        }
#endif

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
