using System.Collections.Generic;
using System.Linq;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Core.Economy;
using DoudizhuTower.Gameplay.Entities;
using DoudizhuTower.Gameplay.Systems;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    public enum WinCondition { DestroyAll, DestroyOne }

    /// <summary>
    /// 战场管理器（partial class，拆分为 3 个文件）。
    /// BattleManager.cs        — 核心字段、初始化、主循环、胜负判定
    /// BattleManager.Spawning.cs — 牌型生成、通用生成、伤害分担
    /// BattleManager.Heroes.cs  — 英雄生成、被动注入、灵骑光环
    /// </summary>
    public partial class BattleManager : MonoBehaviour
    {
        /// <summary>静态实例（供 UnitPassives 等外部组件调用生成方法）</summary>
        public static BattleManager Instance { get; private set; }

        /// <summary>防止胜利/失败重复触发</summary>
        private bool _gameEnded;

        [Header("胜利条件")]
        [SerializeField] private WinCondition _winCondition = WinCondition.DestroyAll;

        // 基地列表由 GameBootstrapper 注入，不再在 Inspector 中重复配置
        private Component[] baseBuildings;

        [Header("工厂")]
        [SerializeField] private UnitFactory unitFactory;

        [Header("英雄")]
        [SerializeField] private CardUnit heroPrefab;
        
        // [已废弃] 英雄配置现在由预制体上的 HeroUnitConfig 组件提供
        // [SerializeField] private HeroType _selectedHero = HeroType.Blademaster;
        // [SerializeField] private DoudizhuTower.Config.HeroConfig _heroConfig;
        // [SerializeField] private bool _useHeroPrefabStats = true;

        [Header("状态机")]
        [SerializeField] private GameStateMachine _gameStateMachine;

        [Header("暴君光环")]
        [SerializeField] private bool _enableTyrantAura = true;
        [Tooltip("地主兵种 HP 倍率")]
        [SerializeField] private float _tyrantHpMultiplier = 1.2f;
        [Tooltip("地主兵种 ATK 倍率")]
        [SerializeField] private float _tyrantAtkMultiplier = 1.15f;
        [Tooltip("地主击杀额外金币比例")]
        [SerializeField] private float _killGoldBonusPct = 0.1f;

        [Header("顺子加速")]
        [Tooltip("普通顺子（5 张）攻速倍率（>1=更快，所有单位统一）")]
        [SerializeField] private float _straightSpeedBoost = 1.2f;
        [Tooltip("顺子 6+ 攻速倍率")]
        [SerializeField] private float _straight6SpeedBoost = 1.3f;
        [Tooltip("普通顺子（5 张）移速倍率")]
        [SerializeField] private float _straightMoveSpeed = 1.1f;
        [Tooltip("顺子 6+ 移速倍率")]
        [SerializeField] private float _straight6MoveSpeed = 1.15f;

        [Header("合击参数")]
        [Tooltip("对子生成的单位攻击力加成 (0.5 = +50% 攻击力，两个单位都生效)")]
        [SerializeField] private float _jointDamageBonus = 0.5f;

        [Header("分担参数")]
        [Tooltip("三张分担检测范围")]
        [SerializeField] private float _shareRange = 5f;
        [Tooltip("分担：最高血量单位承受比例 (0.6 = 60%)")]
        [SerializeField] private float _shareMainPct = 0.6f;
        [Tooltip("分担：其他单位各承受比例 (0.2 = 20%)")]
        [SerializeField] private float _shareOtherPct = 0.2f;

        private EconomyManager _economyManager;

        // ─── 对局统计访问器 ──────────────────────────────
        public int CardsPlayedCount => _cardsPlayedCount;
        public int UnitsSpawnedCount => _unitsSpawnedCount;
        public int UnitsKilledCount => _unitsKilledCount;
        public float GoldEarnedTotal => _goldEarnedTotal;

        /// <summary>由 EconomyManager 调用，累计本局获得的金币</summary>
        public void TrackGoldEarned(float amount) => _goldEarnedTotal += amount;

        /// <summary>单位生成事件（用于 UI 层挂钩飘字/信息面板等）</summary>
        public event System.Action<CardUnit> OnUnitSpawned;
        /// <summary>单位死亡事件（用于网络广播 UNIT_DIED）</summary>
        public event System.Action<int> OnUnitDiedEvent;

        /// <summary>游戏结束事件（bool = 玩家是否胜利）</summary>
        public event System.Action<bool> OnGameEnded;

        // ─── 对局统计 ──────────────────────────────────
        private int _cardsPlayedCount;
        private int _unitsSpawnedCount;
        private int _unitsKilledCount;
        private float _goldEarnedTotal;

        private readonly List<CardUnit> _pendingDeaths = new();
        private readonly List<CardUnit> _allUnits = new();
        private readonly Dictionary<int, CardUnit> _unitById = new();
        private int _globalUnitId;
        private IBuildingTarget[] _allBuildingTargets;
        private readonly List<CardUnit> _activeBosses = new();
        private readonly Collider2D[] _overlapCache = new Collider2D[128];

        // ─── 单位注册 ──────────────────────────────────

        private void RegisterUnit(CardUnit unit)
        {
            if (unit != null && !_allUnits.Contains(unit))
            {
                int id = _globalUnitId++;
                unit.SetUnitId(id);
                _allUnits.Add(unit);
                _unitById[id] = unit;
                _unitsSpawnedCount++;
            }
        }

        private void UnregisterUnit(CardUnit unit)
        {
            if (unit != null)
            {
                _allUnits.Remove(unit);
                _unitById.Remove(unit.UnitId);
            }
        }

        // ─── 敌方查询 ──────────────────────────────────

        /// <summary>
        /// 查询指定阵营在指定路线上的存活兵种（供 BuildingAI 路线评估使用）。
        /// </summary>
        public List<CardUnit> GetAliveUnitsOnRoute(RoutePath route, bool isLandlordFaction)
        {
            var result = new List<CardUnit>();
            if (route == null) return result;
            foreach (var u in _allUnits)
            {
                if (u == null || !u.IsAlive) continue;
                if (u.IsLandlord == isLandlordFaction && u.FollowPath == route)
                    result.Add(u);
            }
            return result;
        }

        /// <summary>统一门禁：判断 target 是否是 unit 的合法战斗目标</summary>
        public bool IsValidCombatTarget(CardUnit unit, CardUnit target)
        {
            if (target == null || !target.IsAlive || target == unit) return false;
            var boss = target.GetComponent<BossController>();
            if (boss != null && !boss.IsActive) return false;
            if (target.IsLandlord == unit.IsLandlord) return false;
            if (unit.Lane != Lane.None && target.Lane != Lane.None && target.Lane != unit.Lane) return false;
            return true;
        }

        public List<CardUnit> GetEnemiesFor(CardUnit unit)
        {
            var result = new List<CardUnit>();
            foreach (var other in _allUnits)
            {
                if (!IsValidCombatTarget(unit, other)) continue;
                result.Add(other);
            }
            return result;
        }

        private static bool IsLandlord(Component c)
        {
            var cu = c.GetComponent<CardUnit>();
            return cu != null && cu.IsLandlord;
        }

        // ─── 初始化 ──────────────────────────────────

        private void Start()
        {
            if (unitFactory == null) unitFactory = GameObject.Find("EntityPool")?.GetComponent<UnitFactory>();
            if (baseBuildings == null || baseBuildings.Length == 0)
            {
                // GameBootstrapper 会在 Initialize 前注入 baseBuildings
                // 如果此处仍为空，说明注入流程异常
                Debug.LogWarning("[BattleManager] baseBuildings 未被注入，GameObject.Find 不查找未激活对象");
            }
        }

        /// <summary>
        /// 注入基地列表（由 GameBootstrapper 调用，替代 Inspector 配置）。
        /// 必须在 Initialize() 之前调用。
        /// </summary>
        public void SetBaseBuildings(Component[] buildings)
        {
            baseBuildings = buildings;
        }

        public void Initialize()
        {
            Instance = this;
            var allCardUnits = FindObjectsByType<CardUnit>(FindObjectsSortMode.None);

            var list = new List<IBuildingTarget>();

            foreach (var u in allCardUnits)
            {
                if (u == null) continue;

                if (u._isBuilding && u._isBoss)
                {
                    Debug.LogWarning($"[BattleManager] {u.name} 同时设置了 _isBuilding 和 _isBoss，优先走 BOSS 路径。请检查预制体配置。");
                    RegisterUnit(u);
                    u.OnDied -= OnUnitDied; u.OnDied += OnUnitDied;
                }
                else if (u._isBuilding)
                {
                    list.Add(u);
                    u.OnDestroyed += OnInstallationDestroyed;
                }
                else if (u._isBoss)
                {
                    RegisterUnit(u);
                    u.OnDied -= OnUnitDied; u.OnDied += OnUnitDied;
                }
                else
                {
                    RegisterUnit(u);
                    u.OnDied -= OnUnitDied; u.OnDied += OnUnitDied;
                }
            }

            _allBuildingTargets = list.ToArray();
        }

        /// <summary>注入经济系统（供暴君税赋使用）</summary>
        public void SetEconomyManager(EconomyManager em) => _economyManager = em;

        // ─── BOSS 系统 ──────────────────────────────────

        public void ActivateBoss(CardUnit boss, RoutePath route)
        {
            if (boss == null) return;

            bool isLandlord = boss.IsLandlord;
            boss.FollowPath = route;
            if (route != null) SnapToPathStart(boss);
            boss.SetEnemyUnits(GetEnemiesFor(boss));
            boss.SyncBaseScale();

            _activeBosses.Add(boss);
            boss.OnDestroyed += _ =>
            {
                // 缓存路径坐标，防止 BOSS 回池后路径点失效导致单位朝 (0,0) 移动
                if (route != null) route.CachePositions();
                _activeBosses.Remove(boss);
                RecheckVictory();
            };

            OnUnitSpawned?.Invoke(boss);
        }

        public void RegisterBossAsSummoner(CardUnit boss, CardDeck deck,
            float initialGold, float incomeRate, int maxSelection, float drawInterval)
        {
            if (boss == null) return;

            var ai = boss.GetComponent<BuildingAI>();
            if (ai == null)
            {
                Debug.LogWarning("[BattleManager] BOSS 缺少 BuildingAI 组件，跳过召唤师注册");
                return;
            }

            if (boss.GetComponent<RouteGroup>() == null)
                Debug.LogWarning("[BattleManager] BOSS 缺少 RouteGroup 组件，召唤师派兵路线将为空");

            var economy = new EconomySystem(initialGold, incomeRate);
            var hand = new CardHand(20);
            ai.Initialize(hand, economy, this, deck, maxSelection, drawInterval);

            // 注册 BOSS 手牌到 DomainSystem（要不起领域封印依赖此注册）
            var domainSystem = FindFirstObjectByType<DoudizhuTower.Gameplay.Battle.DomainSystem>();
            if (domainSystem != null)
            {
                domainSystem.AddEnemyCardHand(hand);
                Debug.Log("[BattleManager] BOSS CardHand 已注册到 DomainSystem");
            }
        }

        private bool HasAliveEnemyBoss(bool playerIsLandlord)
        {
            foreach (var boss in _activeBosses)
            {
                if (boss != null && boss.IsAlive && boss.IsLandlord != playerIsLandlord)
                    return true;
            }
            return false;
        }

        private void RecheckVictory()
        {
            bool playerIsLandlord = CardUnit.PlayerIsLandlord;
            if (HasAliveEnemyBoss(playerIsLandlord)) return;

            if (_winCondition == WinCondition.DestroyOne)
            {
                if (GetEnemyInstallations(playerIsLandlord).Any(i => i.IsDestroyed))
                    TriggerVictory();
            }
            else if (_winCondition == WinCondition.DestroyAll)
            {
                if (AllEnemyDestroyed(playerIsLandlord))
                    TriggerVictory();
            }
        }

        private bool IsFriendlyInstallation(IBuildingTarget target, bool unitIsLandlord)
        {
            var cu = target.transform.GetComponent<CardUnit>();
            return cu != null && cu.IsLandlord == unitIsLandlord;
        }

        private IEnumerable<IBuildingTarget> GetEnemyInstallations(bool unitIsLandlord)
            => _allBuildingTargets.Where(i => !IsFriendlyInstallation(i, unitIsLandlord));

        private bool AllEnemyDestroyed(bool unitIsLandlord)
        {
            return GetEnemyInstallations(unitIsLandlord).All(i => i.IsDestroyed);
        }

        // ─── 主循环 ───────────────────────────────────

        /// <summary>演出期间暂停战斗逻辑（AI/索敌/攻击决策）</summary>
        public bool IsPresentationActive { get; set; }

        private void Update()
        {
            // 清理死亡单位不受演出状态影响，确保击杀后及时回收
            CleanupDeadUnits();

            if (IsPresentationActive) return;

            foreach (var unit in _allUnits)
                if (unit != null && unit.IsAlive)
                {
                    unit.SetEnemyUnits(GetEnemiesFor(unit));
                    unit.SetEnemyBuildings(_allBuildingTargets);
                }
        }

        private void OnUnitDied(int unitId)
        {
            if (!_unitById.TryGetValue(unitId, out var unit) || unit == null) return;
            _unitsKilledCount++;
            if (_enableTyrantAura && _economyManager != null
                && unit.LastAttacker != null
                && unit.LastAttacker.IsLandlord == CardUnit.PlayerIsLandlord)
            {
                _economyManager.AddGold(unit.Stats.ATK * _killGoldBonusPct);
            }
            _pendingDeaths.Add(unit);
            OnUnitDiedEvent?.Invoke(unitId);
        }

        private void CleanupDeadUnits()
        {
            if (_pendingDeaths.Count == 0) return;
            foreach (var unit in _pendingDeaths)
            {
                if (unit == null) continue;
                UnregisterUnit(unit);
                unitFactory.Despawn(unit);
            }
            _pendingDeaths.Clear();
        }

        // ─── 基地事件 ─────────────────────────────────

        private void OnInstallationDestroyed(IBuildingTarget target)
        {
            if (_allBuildingTargets == null) return;

            bool playerIsLandlord = CardUnit.PlayerIsLandlord;
            bool targetIsFriendly = IsFriendlyInstallation(target, playerIsLandlord);

            if (targetIsFriendly)
            {
                if (AllFriendlyDestroyed(playerIsLandlord))
                    TriggerDefeat();
                return;
            }

            if (HasAliveEnemyBoss(playerIsLandlord))
                return;

            if (_winCondition == WinCondition.DestroyOne)
                TriggerVictory();
            else if (AllEnemyDestroyed(playerIsLandlord))
                TriggerVictory();
        }

        private void TriggerVictory()
        {
            if (_gameEnded) return;
            _gameEnded = true;
            _gameStateMachine?.TransitionTo(GamePhase.GameOver);
            OnGameEnded?.Invoke(true);
        }

        internal void TriggerDefeat()
        {
            if (_gameEnded) return;
            _gameEnded = true;
            _gameStateMachine?.TransitionTo(GamePhase.GameOver);
            OnGameEnded?.Invoke(false);
        }

        private bool AllFriendlyDestroyed(bool playerIsLandlord)
        {
            return _allBuildingTargets
                .Where(i => i != null && IsFriendlyInstallation(i, playerIsLandlord))
                .All(i => i.IsDestroyed);
        }

        private void OnEnable()
        {
            GameSession.OnRuntimeReset += ResetRuntime;
        }

        private void OnDisable()
        {
            GameSession.OnRuntimeReset -= ResetRuntime;
        }

        private void ResetRuntime()
        {
            _allUnits.Clear();
            _unitById.Clear();
            _allBuildingTargets = null;
            _activeBosses.Clear();
            _pendingDeaths.Clear();
            _globalUnitId = 0;
        }
    }
}
