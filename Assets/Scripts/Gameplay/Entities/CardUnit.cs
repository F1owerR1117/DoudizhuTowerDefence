using System;
using System.Collections.Generic;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Gameplay.Battle;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>高度标签：地面 / 空中</summary>
    [System.Flags]
    public enum UnitHeight
    {
        Ground = 1 << 0,
        Air    = 1 << 1,
    }

    /// <summary>
    /// 兵种基类。
    /// 管理移动、攻击、索敌、2.5D 视觉补正。
    /// 子类通过重写 OnUpdate() 实现特化行为。
    ///
    /// 使用 partial class 拆分为多个文件：
    /// - CardUnit.cs           : 核心数据、属性、初始化
    /// - CardUnit.Movement.cs  : 移动逻辑
    /// - CardUnit.Combat.cs    : 攻击和索敌逻辑
    /// - CardUnit.Animation.cs : 动画控制、对象池管理
    /// </summary>
    public partial class CardUnit : MonoBehaviour, IBuildingTarget, Core.Lifecycle.IRuntimeReady
    {
        /// <summary>运行时就绪：已初始化 + Stats 已赋值</summary>
        public bool IsRuntimeReady => _initialized && Stats.HP > 0f;

        /// <summary>玩家所属阵营，血条根据此判断颜色</summary>
        public static bool PlayerIsLandlord = true;

        [Header("运行时数据 (只读)")]
        [SerializeField] private int _unitId;
        [SerializeField] private float _currentHP;
        [SerializeField] private Lane _lane;
        [SerializeField] private bool _isLandlord;

        /// <summary>拥有此单位的玩家 PlayerId（网络身份，由 GameSession 分配）</summary>
        public int OwnerPlayerId { get; set; } = -1;

        [Header("远程攻击设置")]
        [SerializeField] private bool _isRanged;
        [Header("高度系统")]
        [Tooltip("本兵种的高度标签")]
        [SerializeField] private UnitHeight _unitHeight = UnitHeight.Ground;
        [Tooltip("本兵种可攻击的高度（多选）")]
        [SerializeField] private UnitHeight _canAttackHeight = UnitHeight.Ground | UnitHeight.Air;
        [Tooltip("本兵种可阻挡的高度（多选，默认与可攻击高度一致）")]
        [SerializeField] private UnitHeight _canBlockHeight = UnitHeight.Ground | UnitHeight.Air;

        // 运行时高度覆盖
        private bool _heightOverridden;
        private UnitHeight _originalUnitHeight;
        private UnitHeight _blockableByHeight;

        // 运行时「可攻击目标高度」覆盖（如冲锋期间限制可打目标）
        private bool _attackableOverridden;
        private UnitHeight _originalCanAttackHeight;
        [Header("索敌范围")]
        [Tooltip("索敌检测范围（0 = 使用攻击范围）。点杀等特化索敌使用此值扩展搜索。")]
        [SerializeField] private float _detectionRange;

        [Header("建筑模式（静止结构，可被攻击、影响胜负）")]
        public bool _isBuilding;
        [Tooltip("建筑每秒回血量（仅 _isBuilding 时生效）")]
        [SerializeField] private float _regenPerSecond;
        [Header("BOSS 模式（可移动，阻断胜利条件）")]
        public bool _isBoss;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private Projectile _projectilePrefab;

        [Header("兵种属性 (在预制体 Inspector 中修改)")]
        [Tooltip("生命值")]
        [SerializeField] private float _hp = 100;
        [Tooltip("攻击力")]
        [SerializeField] private float _atk = 10;
        [Tooltip("攻击间隔（秒）")]
        [SerializeField] private float _attackInterval = 1.2f;
        [Tooltip("移动速度")]
        [SerializeField] private float _moveSpeed = 2.8f;
        [Tooltip("攻击范围")]
        [SerializeField] private float _range = 1.8f;
        [Tooltip("单次攻击动画的打击帧数（双刀填 2）")]
        [SerializeField] private int _hitCount = 1;
        [Tooltip("同时攻击的最大目标数（1=单目标，>1=多目标）")]
        [SerializeField] private int _maxTargets = 1;
        [Tooltip("多目标搜索半径（0=使用攻击范围）")]
        [SerializeField] private float _multiTargetRadius = 0f;

        /// <summary>最大同时攻击目标数</summary>
        public int MaxTargets => _maxTargets;

        /// <summary>所属预制体（对象池回收用，由 UnitFactory 设置）</summary>
        [System.NonSerialized] public CardUnit SourcePrefab;



        // ★ Core 层数据块（由预制体字段组装）
        public SoldierStats Stats { get; protected set; }

        /// <summary>是否正在引导（召唤等）：为 true 时本帧不移动、不攻击</summary>
        public bool IsChanneling { get; set; }

        /// <summary>
        /// 编辑期可用的攻击距离：Edit 模式下未调用 Initialize，Stats 为空或为 0，
        /// Gizmos 需要回退到预制体上的 _range 才能画出正确范围。
        /// </summary>
        public float RangeForPreview => Stats.Range > 0f ? Stats.Range : _range;

        /// <summary>编辑期可用的索敌半径（编辑期 Stats 为 0 时回退到攻击距离）</summary>
        public float DetectionRangeForPreview => _detectionRange > 0f ? _detectionRange : RangeForPreview;

        /// <summary>
        /// 编辑期可用的碰撞箱中心：Awake 未执行时 _collider 为 null，退化为查找子物体上的碰撞体，
        /// 保证编辑期与运行期的圆环中心一致（部分兵种碰撞箱有偏移）。
        /// </summary>
        public Vector2 PreviewCenter
        {
            get
            {
                var col = _collider != null ? _collider : GetComponentInChildren<Collider2D>();
                return col != null ? (Vector2)col.bounds.center : (Vector2)transform.position;
            }
        }

        /// <summary>编辑期可用的碰撞箱尺寸（世界空间）</summary>
        public Vector2 PreviewColliderSize
        {
            get
            {
                var col = _collider != null ? _collider : GetComponentInChildren<Collider2D>();
                return col != null ? (Vector2)col.bounds.size : new Vector2(1f, 1f);
            }
        }

        private UnitPassives _passives;
        private bool _passivesCached;
        /// <summary>被动组件（懒加载并缓存，含「没有该组件」的负缓存，避免每帧 GetComponent）</summary>
        public UnitPassives Passives
        {
            get
            {
                if (!_passivesCached)
                {
                    _passives = GetComponent<UnitPassives>();
                    _passivesCached = true;
                }
                return _passives;
            }
        }

        /// <summary>
        /// 建筑初始化血量（替代 Installation.InitHP）。
        /// 直接设置 _hp 字段并重建 Stats，用于建筑预制体的 Start 初始化。
        /// </summary>
        public void InitBuildingHP(float hp)
        {
            _hp = hp;
            Stats = new SoldierStats
            {
                Rank = Stats.Rank,
                HP = hp,
                ATK = Stats.ATK,
                AttackInterval = Stats.AttackInterval,
                MoveSpeed = 0f,
                Range = Stats.Range,
                CollisionRadius = Stats.CollisionRadius
            };
            _currentHP = hp;
            OnHPChanged?.Invoke(_unitId, _currentHP);
        }

        /// <summary>直接设置基础 Stats，清除所有已有 Buff（用于英雄属性覆盖等永久性修改）。</summary>
        public void SetStats(SoldierStats stats)
        {
            float ratio = Stats.HP > 0f ? _currentHP / Stats.HP : 1f;
            Stats = stats;
            _baseStats = stats;
            _hasBaseStats = true;
            _buffs.Clear();
            _currentHP = stats.HP * ratio;
            OnHPChanged?.Invoke(_unitId, _currentHP);
        }

        public int UnitId => _unitId;
        public Lane Lane => _lane;
        public bool IsAlive => _currentHP > 0f;

        /// <summary>飞扑进行中（位置由飞扑协程驱动，期间跳过常规移动/攻击决策）</summary>
        public bool IsPouncing { get; set; }

        /// <summary>重新吸附到最近路径点（飞扑脱离路径后调用）</summary>
        public void ResnapToClosestPath() => ResnapToClosestPathDistance();
        public float CurrentHP => _currentHP;
        public float MaxHP => Stats.HP;
        public float HPRatio => Stats.HP > 0f ? _currentHP / Stats.HP : 0f;
        public virtual float CurrentATK => Stats.ATK; // 阶段一无 buff 修正
        public bool IsLandlord => _isLandlord;

        /// <summary>运行时强制设置阵营（供 GameBootstrapper 纠正预置建筑的 Inspector 默认值）</summary>
        public void SetLandlord(bool isLandlord) => _isLandlord = isLandlord;

        /// <summary>运行时设置单位 ID（由 BattleManager.RegisterUnit 调用，保证全局唯一）</summary>
        public void SetUnitId(int id) => _unitId = id;

        /// <summary>治疗事件（参数：本次治疗量）。供飘字显示绿色 +N 使用。</summary>
        public event System.Action<float> OnHealed;

        /// <summary>被处决事件（无参数）。供飘字显示黄色「处决」使用。</summary>
        public event System.Action OnExecuted;

        /// <summary>治疗（不超过 MaxHP）</summary>
        public void Heal(float amount)
        {
            // v2.0: 仅 Master 端修改 HP
            if (!SimulatesCombat) return;
            if (amount <= 0f) return;
            _currentHP = Mathf.Min(_currentHP + amount, Stats.HP);
            OnHPChanged?.Invoke(_unitId, _currentHP);
            // 飘字显示治疗量本身（溢出也照常显示，由需求确认）
            OnHealed?.Invoke(amount);
        }

        // 目标
        public CardUnit Target { get; protected set; }

        /// <summary>最后攻击者（用于暴君税赋击杀追踪）</summary>
        public CardUnit LastAttacker { get; set; }

        /// <summary>击杀事件（由 Die() 触发，参数：被击杀的单位）。供召唤师等被动监听。</summary>
        public event System.Action<CardUnit> OnKillEvent;

        /// <summary>召唤者引用（非 null 表示此单位是被召唤的）</summary>
        public CardUnit Summoner { get; set; }

        /// <summary>是否为嘲讽源（诱饵/坦克），敌人将优先攻击此单位</summary>
        public bool IsTauntSource { get; set; }
        /// <summary>嘲讽光环半径（敌方必须同时落在光环半径与攻击范围的交集内才生效）</summary>
        public float TauntRadius { get; set; }

        /// <summary>屏障层数（诱饵屏障效果），每层吸收一次攻击全部伤害</summary>
        public int ShieldBlocks { get; set; }

        /// <summary>剩余伤害吸收量（诱饵护盾/帝王盾），扣到 0 后正常扣血</summary>
        public float DamageAbsorbRemaining { get; set; }

        /// <summary>不可选取状态（BossSkillSystem 施法期间设置，免疫所有伤害）</summary>
        public bool Invulnerable { get; set; }

        /// <summary>眩晕计时器（>0 时无法行动），由骑兵等技能设置。仅 Master 写入。</summary>
        public float StunTimer { get; set; }

        /// <summary>Client 端视觉眩晕计时器（仅用于动画表现，不参与逻辑判断）</summary>
        public float VisualStunTimer { get; set; }

        /// <summary>伤害减免乘数（0=无减免，0.5=减半），由重骑兵/铁骑兵设置</summary>
        public float DamageReduction { get; set; }

        /// <summary>是否参与战斗模拟（Master=true, Client 联机=false）。Client 仅做视觉表现。</summary>
        public bool SimulatesCombat { get; set; } = true;
        /// <summary>新生成单位的默认 SimulatesCombat 值</summary>
        public static bool SimulatesCombatDefault { get; set; } = true;

        /// <summary>
        /// 兵种标识：流血/燃烧/撕裂按此合并「来源」。
        /// 优先用预制体上填写的 _unitTypeId（改名字不会影响）；
        /// 留空则回退到卡牌点数（同样与名字无关）。
        /// </summary>
        [Tooltip("兵种标识（留空 = 用卡牌点数）。同标识的单位施加的持续状态会合并为同一来源")]
        [SerializeField] private string _unitTypeId;

        private string _unitTypeKeyCached;

        /// <summary>兵种标识（用于被动按兵种合并来源）</summary>
        public string UnitTypeKey
        {
            get
            {
                if (_unitTypeKeyCached != null) return _unitTypeKeyCached;
                _unitTypeKeyCached = !string.IsNullOrEmpty(_unitTypeId)
                    ? _unitTypeId
                    : ((int)Stats.Rank).ToString();
                return _unitTypeKeyCached;
            }
        }

        /// <summary>减速恢复计时器（>0 时标记移速被临时修改）</summary>
        public float SlowRestoreTimer { get; set; }
        /// <summary>当前生效的减速光环移速乘数（1 = 无减速）。多光环时记录最强的那个，便于比较。</summary>
        public float SlowAuraMoveMult { get; set; } = 1f;
        /// <summary>减速前原始移速</summary>
        public float OriginalMoveSpeed { get; set; }

        // ─── 统一 Buff 系统 ─────────────────────────────
        /// <summary>单个 Buff 的属性乘数（1.0 = 无影响）</summary>
        public struct StatBuff
        {
            public float AtkIntervalMult;
            public float MoveSpeedMult;
            public float HpMult;
            public float AtkMult;
            public float RangeMult;
            public StatBuff(float atkInterval = 1f, float moveSpeed = 1f, float hp = 1f, float atk = 1f, float range = 1f)
            {
                AtkIntervalMult = atkInterval; MoveSpeedMult = moveSpeed;
                HpMult = hp; AtkMult = atk; RangeMult = range;
            }
        }
        private readonly Dictionary<string, StatBuff> _buffs = new();
        private SoldierStats _baseStats;
        private bool _hasBaseStats;

        /// <summary>应用/更新一个命名 Buff，自动从基础属性重新计算最终 Stats。</summary>
        public void ApplyBuff(string buffId, StatBuff buff)
        {
            // v2.0: 仅 Master 端修改 Buff 状态
            if (!SimulatesCombat) return;
            if (!_hasBaseStats) { _baseStats = Stats; _hasBaseStats = true; }
            _buffs[buffId] = buff;
            RecalculateStats();
        }

        /// <summary>移除指定 Buff。</summary>
        public void RemoveBuff(string buffId)
        {
            // v2.0: 仅 Master 端修改 Buff 状态
            if (!SimulatesCombat) return;
            if (_buffs.Remove(buffId))
                RecalculateStats();
        }

        /// <summary>从基础属性叠加所有 Buff 计算最终 Stats。</summary>
        private void RecalculateStats()
        {
            float hpRatio = Stats.HP > 0f ? _currentHP / Stats.HP : 1f;
            var s = _baseStats;
            foreach (var kvp in _buffs)
            {
                var b = kvp.Value;
                s.AttackInterval *= b.AtkIntervalMult;
                s.MoveSpeed *= b.MoveSpeedMult;
                s.HP *= b.HpMult;
                s.ATK *= b.AtkMult;
                s.Range *= b.RangeMult;
            }
            Stats = s;
            _currentHP = Stats.HP * hpRatio;
            // v2.0: 属性变化触发 OnStatsChanged，不触发 OnHPChanged
            OnStatsChanged?.Invoke();
        }

        /// <summary>
        /// 碰撞箱视觉中心。所有距离/瞄准/击退计算统一使用此值，禁止直接读取 transform.position。
        /// </summary>
        public Vector2 VisualCenter => _collider != null ? _collider.bounds.center : (Vector2)transform.position;

        /// <summary>分担伤害已重定向标记（防止原伤害重复扣除）</summary>
        public bool ShareRedirected { get; set; }
        /// <summary>分担后的伤害值（>0 时替代原始伤害）</summary>
        public float SharedDamageOverride { get; set; }

        /// <summary>是否为远程单位（弓骑兵需要动态设置）</summary>
        public bool IsRanged { get => _isRanged; set => _isRanged = value; }

        /// <summary>索敌检测范围。0 时使用攻击范围，大于 0 时用于点杀等特化索敌。</summary>
        public float DetectionRange => _detectionRange > 0f ? _detectionRange : Stats.Range;

        /// <summary>本兵种的高度标签（含运行时覆盖）</summary>
        public UnitHeight UnitHeight => _unitHeight;

        /// <summary>本兵种可被哪些高度阻挡（含运行时覆盖，默认等于 UnitHeight）</summary>
        public UnitHeight BlockableByHeight => _heightOverridden ? _blockableByHeight : _unitHeight;

        /// <summary>
        /// 临时覆盖高度标签（如冲锋时视为空中单位）。
        /// </summary>
        /// <param name="height">新的高度标签（影响攻击/索敌判定）</param>
        /// <param name="blockableBy">可被哪些高度阻挡（影响阻挡判定，0 = 使用 height）</param>
        public void SetHeightOverride(UnitHeight height, UnitHeight blockableBy = 0)
        {
            if (!_heightOverridden)
            {
                _originalUnitHeight = _unitHeight;
                _heightOverridden = true;
            }
            _unitHeight = height;
            _blockableByHeight = blockableBy != 0 ? blockableBy : height;
        }

        /// <summary>恢复原始高度标签</summary>
        public void ClearHeightOverride()
        {
            if (_heightOverridden)
            {
                _unitHeight = _originalUnitHeight;
                _heightOverridden = false;
            }
        }

        /// <summary>本兵种是否可攻击指定高度的目标</summary>
        public bool CanAttackHeight(UnitHeight targetHeight) => (_canAttackHeight & targetHeight) != 0;

        /// <summary>
        /// 临时覆盖「可攻击的目标高度」（如冲锋期间只能打地面单位）。
        /// mask 为 0 时不生效。
        /// </summary>
        public void SetAttackableHeightOverride(UnitHeight mask)
        {
            if (mask == 0) return;
            if (!_attackableOverridden)
            {
                _originalCanAttackHeight = _canAttackHeight;
                _attackableOverridden = true;
            }
            _canAttackHeight = mask;
        }

        /// <summary>恢复原始的「可攻击的目标高度」</summary>
        public void ClearAttackableHeightOverride()
        {
            if (!_attackableOverridden) return;
            _canAttackHeight = _originalCanAttackHeight;
            _attackableOverridden = false;
        }

        /// <summary>本兵种是否会被指定高度的目标阻挡</summary>
        public bool CanBlockHeight(UnitHeight targetHeight) => (_canBlockHeight & targetHeight) != 0;

        /// <summary>高亮/取消高亮（改变 SpriteRenderer 颜色叠加）</summary>
        public void SetHighlighted(bool on)
        {
            if (_spriteRenderer != null)
                _spriteRenderer.color = on ? new Color(1f, 1f, 0.4f, 1f) : Color.white;
        }

        /// <summary>当前攻击的建筑目标（动态检测，非静态队列）</summary>
        public IBuildingTarget CurrentTarget { get; private set; }

        private Collider2D _collider;
        public Collider2D Collider2D => _collider;

        /// <summary>路线路径（可选），设置后兵种沿路径行走而非直线</summary>
        [System.NonSerialized] public DoudizhuTower.Gameplay.Battle.RoutePath FollowPath;
        public DoudizhuTower.Core.Cards.CardType SourceCardType { get; set; }
        private float _pathDistance;

        // 组件
        protected SpriteRenderer _spriteRenderer;
        protected Animator _animator;
        protected SimpleAnimator _simpleAnimator;
        protected Vector3 _baseScale = Vector3.one;

        // 朝向
        private bool _facingRight = true;

        /// <summary>当前是否朝向右方（攻击前会朝目标转向，因此攻击瞬间的朝向是准确的）</summary>
        public bool FacingRight => _facingRight;

        // 动画驱动攻击状态
        private bool _isAttacking;
        private CardUnit _attackTarget;
        private int _hitCountDealt;
        private bool _justFinishedAttack;
        /// <summary>正在播放死亡动画，阻止一切非死亡动画切换</summary>
        private bool _isDying;

        /// <summary>当前打击帧索引（从 0 开始，OnAttackEvent 触发时可用）</summary>
        public int CurrentHitFrame => _hitCountDealt;

        /// <summary>是否正在攻击中（攻击动画播放期间）</summary>
        public bool IsAttacking => _isAttacking;

        /// <summary>设置动画播放速度（供召唤等外部系统调用）</summary>
        public void SetAnimSpeedPublic(float speed) => SetAnimSpeed(speed);

        /// <summary>打断当前攻击（眩晕/召唤等控制效果使用）</summary>
        public void InterruptAttack()
        {
            if (!_isAttacking) return;
            _isAttacking = false;
            _attackTarget = null;
            _animDone = false;
            _hitTimelineDone = false;
            _nextHitIndex = 0;
            _attackTimer = 0f;
            _hitTimes = null;
            _hitCountDealt = 0;
            _attackStateTimer = 0f;
            _pendingTauntTarget = null;
            _attackSnapshotTargets = null;
            SetAnimSpeed(1f);
            UpdateAnimatorState(0); // 回到 Idle
        }
        private float _cachedHitNormalizedTime = -1f;
        private bool _animDone;
        private bool _hitTimelineDone;
        private int _nextHitIndex;
        private float _attackTimer;
        private float[] _hitTimes;
        private float _attackStateTimer;
        private CardUnit _pendingTauntTarget;
        private List<CardUnit> _attackSnapshotTargets;

        /// <summary>首帧安全阀：出生第一帧强制索敌，跳过移动，防止盲跑</summary>
        private bool _needsFirstFrameSearch;

        // 本路线上的所有敌方单位（由 BattleManager 注入）
        protected List<CardUnit> _enemyUnits;
        // 所有敌方建筑（由 BattleManager 注入，替代 FindObjectsByType）
        protected IBuildingTarget[] _enemyBuildings;

        // 事件
        public event Action<int, float> OnHPChanged;    // unitId, newHP
        /// <summary>属性变化事件（Buff 应用/移除/属性重算时触发）</summary>
        public event Action OnStatsChanged;
        public event Action<int> OnDied;                // unitId
        /// <summary>IBuildingTarget 摧毁事件（仅 _isBuilding=true 时触发）</summary>
        public event Action<IBuildingTarget> OnDestroyed;

        // ─── IBuildingTarget 实现 ─────────────────────
        bool IBuildingTarget.IsDestroyed => !IsAlive;
        void IBuildingTarget.TakeDamage(float rawDamage)
        {
            TakeDamage(rawDamage, DamageType.Physical);
        }
        Collider2D IBuildingTarget.BuildingCollider => _collider;
        Vector2 IBuildingTarget.LogicCenter => VisualCenter;

        // ─── 统一边缘距离计算 ──────────────────────────

        /// <summary>
        /// 通用边缘距离计算（兼容小兵、建筑、任何 IBuildingTarget）。
        /// 使用 ClosestPoint + bounds.Intersects 安全阀，零 GetComponent。
        /// </summary>
        private float GetEdgeDistance(IBuildingTarget target)
        {
            if (target == null) return float.MaxValue;
            var targetCol = target.BuildingCollider;
            if (_collider == null || targetCol == null)
                return Vector2.Distance(VisualCenter, target.LogicCenter);

            // 安全阀：碰撞箱重叠 → 边缘距为 0
            if (_collider.bounds.Intersects(targetCol.bounds))
                return 0f;

            Vector2 targetEdge = targetCol.ClosestPoint(VisualCenter);
            Vector2 myEdge = _collider.ClosestPoint(targetEdge);
            return Vector2.Distance(myEdge, targetEdge);
        }

        /// <summary>
        /// 单位间边缘距离（bounds.Intersects 优先判定，ClosestPoint 退化兜底）。
        /// 与 GetEdgeDistance(IBuildingTarget) 逻辑一致，但避免 IBuildingTarget 装箱。
        /// </summary>
        public float GetUnitEdgeDistance(CardUnit other)
        {
            if (other == null) return float.MaxValue;
            if (_collider == null || other._collider == null)
                return Vector2.Distance(VisualCenter, other.VisualCenter);

            if (_collider.bounds.Intersects(other._collider.bounds))
                return 0f;

            Vector2 cp = other._collider.ClosestPoint(VisualCenter);
            // ClosestPoint 退化：查询点在碰撞箱内部时返回自身，改用中心距减半径
            if (cp == (Vector2)VisualCenter)
            {
                float d = Vector2.Distance(VisualCenter, other.VisualCenter)
                          - Stats.CollisionRadius - other.Stats.CollisionRadius;
                return Mathf.Max(d, 0f);
            }
            Vector2 cm = _collider.ClosestPoint(cp);
            return Vector2.Distance(cm, cp);
        }

        // ─── 初始化 ───────────────────────────────────

        private void Awake()
        {
            // 预缓存碰撞箱（与 Installation.Awake 保持一致），
            // 确保预置建筑的 BuildingCollider 不为 null
            if (_collider == null)
                _collider = GetComponentInChildren<Collider2D>();

            // 兜底修复：Unity 序列化 [Flags] 枚举组合默认值时可能存为 0（Nothing），
            // 导致单位无法攻击/阻挡任何高度。恢复为合理默认值。
            if (_unitHeight == 0) _unitHeight = UnitHeight.Ground;
            if (_canAttackHeight == 0) _canAttackHeight = UnitHeight.Ground | UnitHeight.Air;
            if (_canBlockHeight == 0) _canBlockHeight = UnitHeight.Ground | UnitHeight.Air;
        }

        public virtual void Initialize(int unitId, CardRank rank, Lane lane, bool isLandlord)
        {
            bool wasInitialized = _initialized;
            _initialized = true;
            _unitId = unitId;
            _lane = lane;
            _isLandlord = isLandlord;

            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _animator = GetComponentInChildren<Animator>(true);
            _simpleAnimator = GetComponentInChildren<SimpleAnimator>();
            _collider = GetComponentInChildren<Collider2D>();

            // CollisionRadius = 碰撞箱较小维度半径（用于兜底回退计算，避免巨型碰撞箱导致数值溢出）
            float autoRadius = 0f;
            if (_collider is BoxCollider2D box)
                autoRadius = Mathf.Min(box.size.x, box.size.y) / 2f;
            else if (_collider is CapsuleCollider2D capsule)
                autoRadius = Mathf.Min(capsule.size.x, capsule.size.y) / 2f;

            // 从预制体 Inspector 字段组装运行时 Stats
            Stats = new SoldierStats
            {
                Rank = rank,
                HP = _hp,
                ATK = _atk,
                AttackInterval = _attackInterval,
                MoveSpeed = _moveSpeed,
                Range = _range,
                CollisionRadius = autoRadius,
                HitCount = _hitCount
            };
            _currentHP = Stats.HP;
            // 同步基础属性（Awake 中 ApplyBuff 可能在 Initialize 之前锁定 _baseStats）
            _baseStats = Stats;
            _hasBaseStats = true;
            _isAttacking = false;
            _attackTarget = null;
            _hitCountDealt = 0;
            _animDone = false;
            _hitTimelineDone = false;
            _nextHitIndex = 0;
            _attackTimer = 0f;
            _hitTimes = null;
            _justFinishedAttack = false;
            _pendingTauntTarget = null;
            _attackSnapshotTargets = null;
            _pathDistance = 0f;
            Target = null;
            CurrentTarget = null;
            _enemyUnits = null;
            _enemyBuildings = null;
            SimulatesCombat = SimulatesCombatDefault;

            _baseScale = transform.localScale;
            _needsFirstFrameSearch = true;
            _initialized = true;
        }

        /// <summary>
        /// 场景预置物体自动初始化：当 Stats == null（未调用 Initialize）时，
        /// 从 Inspector 字段读取属性构建 Stats。
        /// </summary>
        private bool _initialized;

        private void Start()
        {
            if (_initialized) return;

            // 1. 阵营判定 + 基础状态初始化（直接用 Inspector 中的 _isLandlord 字段）
            Initialize(0, CardRank.Three, Lane.None, _isLandlord);
            Debug.Log($"[CardUnit] {name} Start() 未被 Initialize 预调用，使用 Inspector 值 _isLandlord={_isLandlord}。请检查是否为预置建筑。");

            // 2. 血条激活（与 UnitFactory.Spawn 第 70-75 行相同的模式）
            //    血条子物体在预制体中默认 inactive，预置兵种需要在此显式激活并绑定
            var healthBar = GetComponentInChildren<UnitHealthBar>(true);
            if (healthBar != null)
            {
                healthBar.gameObject.SetActive(true);
                healthBar.Initialize(this);
            }
        }

        /// <summary>
        /// 设置单位位置
        /// </summary>
        public void SetPositionSynced(Vector3 position)
        {
            transform.position = position;
        }

        /// <summary>
        /// 同步透视缩放基准值（BattleManager 修改缩放后调用）
        /// </summary>
        public void SyncBaseScale()
        {
            _baseScale = transform.localScale;
        }

        /// <summary>
        /// 注入敌方单位列表（由 BattleManager 每帧更新）
        /// </summary>
        public virtual void SetEnemyUnits(List<CardUnit> enemies)
        {
            _enemyUnits = enemies;
            if (enemies != null)
            {
                bool containsSelf = enemies.Contains(this);
                if (containsSelf)
                    Debug.LogError($"[严重] {name}(ID={_unitId}) SetEnemyUnits 列表包含自身！count={enemies.Count}");
            }
        }

        /// <summary>
        /// 注入敌方建筑列表（由 BattleManager 每帧更新，替代 FindObjectsByType）
        /// </summary>
        public void SetEnemyBuildings(IBuildingTarget[] buildings)
        {
            _enemyBuildings = buildings;
        }

        // ─── 生命周期 ─────────────────────────────────

        protected virtual void Update()
        {
            if (!IsRuntimeReady) return;
            if (!IsAlive) return;

            // v2.0: 建筑回血仅在 Master 端执行
            if (SimulatesCombat && _isBuilding && _regenPerSecond > 0f && _currentHP < Stats.HP)
            {
                _currentHP = Mathf.Min(Stats.HP, _currentHP + _regenPerSecond * Time.deltaTime);
                OnHPChanged?.Invoke(_unitId, _currentHP);
            }

            // 减速恢复倒计时
            if (SlowRestoreTimer > 0f)
            {
                SlowRestoreTimer -= Time.deltaTime;
                if (SlowRestoreTimer <= 0f)
                {
                    OriginalMoveSpeed = 0f;
                    SlowAuraMoveMult = 1f;   // 复位，允许下一个光环重新按强度竞争
                    RefreshSlowBuff();       // 与损伤通道取更强后重新应用
                }
            }

            // 流血 / 燃烧 / 撕裂 / 损伤结算
            UpdateDoT();

            // v2.0: 眩晕递减仅在 Master 端执行
            if (SimulatesCombat && StunTimer > 0f)
            {
                StunTimer -= Time.deltaTime;
                InterruptAttack();
                return;
            }

            // 首帧安全阀：强制索敌一次，跳过移动，防止盲跑
            if (_needsFirstFrameSearch)
            {
                _needsFirstFrameSearch = false;
                Debug.Log($"[FIRST_FRAME] {name} lane={_lane} enemyUnitsNull={_enemyUnits == null} currentTarget={CurrentTarget?.GetType().Name} target={Target?.name}");
                UpdateTarget();
                return;
            }

            OnUpdate();

            // ─── 视觉诊断：画出寻路目标 vs 实际位置 ───
            if (FollowPath != null && FollowPath.waypoints != null && FollowPath.waypoints.Length >= 2)
            {
                MapPathDiagnostics();
            }
        }

        private static bool _queueProcessedThisFrame;
        private static int _lastProcessedFrame = -1;

        /// <summary>
        /// 帧末结算伤害队列（每帧只执行一次，由第一个活跃的 CardUnit 触发）。
        /// 确保同帧内所有攻击意图统一结算，消除 Update 执行顺序对战斗结果的影响。
        /// </summary>
        private void LateUpdate()
        {
            if (_lastProcessedFrame == Time.frameCount) return;
            _lastProcessedFrame = Time.frameCount;
            DamageQueue.ProcessAll();
        }

        /// <summary>
        /// 子类重写此方法实现特化行为（人海连击/盾墙/点杀/冲锋/光环）
        /// </summary>
        protected virtual void OnUpdate()
        {
            // Fusion 模式：禁用旧战斗逻辑
            if (SimulationDisabled) return;

            // 建筑静止：不移动、不攻击、不索敌
            if (_isBuilding) return;

            // ── Client 视觉模式：只做路径行军，不做战斗决策 ──
            if (!SimulatesCombat)
            {
                UpdateAnimatorState(1);
                MoveTowardEnemyBase();
                return;
            }

            // ── Stats 未初始化：跳过战斗逻辑 ──
            if (Stats.HP <= 0f) return;

            // ── 飞扑中：位置由飞扑协程驱动，跳过一切移动/攻击决策 ──
            if (IsPouncing) return;

            // ── 攻击中 → Timeline 驱动攻击帧 ──
            if (_isAttacking)
            {
                // 1. 超时安全阀 → 强制中断（仅防卡死，不参与正常退出）
                if (Stats.HP > 0f && _attackStateTimer > Stats.AttackInterval * 3f)
                {
                    Debug.LogWarning($"[AttackStuck] {name} 攻击超时重置: hitDealt={_hitCountDealt}/{Stats.HitCount}, animDone={_animDone}, timelineDone={_hitTimelineDone}, target={_attackTarget != null}");
                    InterruptAttack();
                    return;
                }

                _attackStateTimer += Time.deltaTime;

                // 2. Timeline 驱动攻击帧
                UpdateAttackTimeline();

                // 3. 非 Invulnerable：嘲讽记录（不中断，攻击结束后切换）
                if (!Invulnerable)
                {
                    var tauntDuringAttack = FindNearestTauntSourceFor(this);
                    if (tauntDuringAttack != null && tauntDuringAttack != _attackTarget)
                        _pendingTauntTarget = tauntDuringAttack;
                }

                // 4. 攻击正常完成：动画播完 + Timeline 完成
                if (IsAttackAnimDone()) _animDone = true;
                if (_animDone && _hitTimelineDone)
                {
                    _isAttacking = false;
                    _attackTarget = null;
                    _attackStateTimer = 0f;
                    SetAnimSpeed(1f);
                    UpdateAnimatorState(0);
                    _justFinishedAttack = true;
                    _animDone = false;
                    _hitTimelineDone = false;
                    _nextHitIndex = 0;
                    _attackTimer = 0f;
                    _hitTimes = null;
                    _attackSnapshotTargets = null;
                    return;
                }

                return;
            }

            // ── 攻击刚结束 → 跳过 1 帧让 Animator 回到 Idle，再重新索敌 ──
            if (_justFinishedAttack)
            {
                _justFinishedAttack = false;

                // 嘲讽延迟切换：攻击结束后优先攻击嘲讽目标
                if (_pendingTauntTarget != null && _pendingTauntTarget.IsAlive)
                {
                    Target = _pendingTauntTarget;
                    _pendingTauntTarget = null;
                }
                else
                {
                    _pendingTauntTarget = null;
                }
                return;
            }

            // ── 引导中（如召唤师播放召唤动画）→ 原地站定，不移动也不攻击 ──
            // 由 UnitPassives 在召唤开始时置位、召唤帧/取消时复位。
            if (IsChanneling)
            {
                UpdateAnimatorState(0);
                return;
            }

            // ── 动态建筑检测：清除已摧毁的目标 ──
            if (CurrentTarget != null && CurrentTarget.IsDestroyed)
                CurrentTarget = null;

            // ── 建筑锁定检测：已在建筑攻击范围内则锁定 ──
            bool buildingLocked = false;
            if (CurrentTarget != null && !CurrentTarget.IsDestroyed)
            {
                if (GetEdgeDistance(CurrentTarget) <= Stats.Range)
                {
                    buildingLocked = true;
                    Target = null;
                }
            }

            // ── 没有建筑目标时，动态搜索附近敌方建筑 ──
            if (!buildingLocked && CurrentTarget == null)
            {
                var nearbyBuilding = FindNearestEnemyBuilding();
                if (nearbyBuilding != null)
                {
                    CurrentTarget = nearbyBuilding;
                    if (GetEdgeDistance(CurrentTarget) <= Stats.Range)
                    {
                        buildingLocked = true;
                        Target = null;
                    }
                }
            }

            // 嘲讽优先级高于建筑：攻击范围内的嘲讽源强制索敌
            var tauntSource = FindNearestTauntSourceFor(this);
            if (tauntSource != null)
            {
                buildingLocked = false;
                Target = tauntSource;
            }
            else if (!buildingLocked)
            {
                UpdateTarget();
            }

            // ── 治疗优先：把受伤友军作为攻击目标（走完整攻击流程：走位 → 攻击动画 → 攻击帧治疗） ──
            var healCtl = Passives;
            if (healCtl != null)
            {
                var healTarget = healCtl.AcquireHealTarget();
                if (healTarget != null)
                {
                    if (healTarget != this)
                        UpdateFacing(healTarget.VisualCenter - VisualCenter);

                    // 已在攻击距离内 → 发起「治疗攻击」
                    if (GetUnitEdgeDistance(healTarget) <= Stats.Range)
                    {
                        TryAttack(healTarget);
                        return;
                    }
                    // 不在距离内 → 走过去（同路限制与追击敌人一致）
                    if (_lane == Lane.None || healTarget._lane == _lane)
                    {
                        UpdateAnimatorState(1);
                        MoveTowardTarget(healTarget);
                        return;
                    }
                    // 跨路且追不到 → 放弃治疗，回退正常战斗
                }
            }

            // 1. 有敌方单位且在射程内 → 攻击并站桩
            if (!buildingLocked && Target != null && Target.IsAlive && IsTargetInRange(Target))
            {
                UpdateFacing(Target.VisualCenter - VisualCenter);
                TryAttack(Target);
                return;
            }

            // 2. 有敌方单位但不在射程内 → 先尝试飞扑，否则同路追击
            if (!buildingLocked && Target != null && Target.IsAlive && !IsTargetInRange(Target))
            {
                // 飞扑：目标在射程外、且在飞扑范围内 → 抛物线跃到射程边缘
                var pounceCtl = Passives;
                if (pounceCtl != null && pounceCtl.TryStartPounce(Target)) return;

                if (_lane == Lane.None || Target._lane == _lane)
                {
                    UpdateAnimatorState(1);
                    MoveTowardTarget(Target);
                    return;
                }
            }

            // ── 战斗结束：路径重投影，防止掉头 ──
            ResnapToClosestPathDistance();

            // 3. 有目标建筑且到达附近 → 攻击建筑（嘲讽源存在时跳过）
            bool hasTauntTarget = Target != null && Target.IsTauntSource;
            if (!hasTauntTarget && CurrentTarget != null && !CurrentTarget.IsDestroyed)
            {
                // 防御：若目标建筑是友方则清除（防止因初始化时序导致误锁）
                if (CurrentTarget is CardUnit targetCU && targetCU.IsLandlord == IsLandlord)
                {
                    CurrentTarget = null;
                    return;
                }
                if (GetEdgeDistance(CurrentTarget) <= Stats.Range)
                {
                    if (!_isAttacking)
                    {
                        _isAttacking = true;
                        _hitCountDealt = 0;
                        _animDone = false;
                        _hitTimelineDone = false;
                        _nextHitIndex = 0;
                        _attackTimer = 0f;
                        _hitTimes = null;
                        _pendingTauntTarget = null;
                        _attackSnapshotTargets = null;
                        float interval = Stats.AttackInterval;
                        float clipLen = GetAttackClipLength();
                        float speed = clipLen > 0f ? Mathf.Min(clipLen / interval, 4f) : 1f;
                        SetAnimSpeed(speed);
                        UpdateAnimatorState(2);
                        // Timeline 已在 TryAttack 中创建（此路径为建筑攻击，手动设置 Timeline）
                        _hitTimes = new float[] { 1f };
                        _nextHitIndex = 0;
                        _attackTimer = 0f;
                        _hitTimelineDone = false;
                    }
                    return;
                }
            }

            // 4. 朝目标移动
            UpdateAnimatorState(1);
            MoveTowardEnemyBase();
        }

        // ─── 2.5D 视觉补正（§5a） ────────────────────

    }
}
