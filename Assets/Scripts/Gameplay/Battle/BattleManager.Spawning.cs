using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Gameplay.Entities;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    public partial class BattleManager
    {
        // ─── 牌型入口 ──────────────────────────────────

        public void DeployCards(Card[] cards, CardTypeResult result, RouteGroup routeGroup, Component sourceBase)
        {
            if (!result.IsValid) { Debug.LogWarning("[BattleManager] 尝试部署无效牌型"); return; }
            _cardsPlayedCount += cards.Length;
            Lane lane = Lane.None;

            switch (result.Type)
            {
                case CardType.Bomb: SpawnBomb(cards, result, lane, sourceBase); break;
                case CardType.Pair: SpawnPair(cards, lane, sourceBase); break;
                case CardType.Triple: SpawnTriple(cards, lane, sourceBase); break;
                case CardType.TripleWithOne: SpawnTripleWithOne(cards, result, lane, sourceBase); break;
                case CardType.TripleWithPair: SpawnTripleWithPair(cards, result, lane, sourceBase); break;
                case CardType.Straight:
                case CardType.Straight6Plus: SpawnStraight(cards, lane, sourceBase); break;
                case CardType.ConsecutivePair: SpawnConsecutivePair(cards, result, lane, sourceBase); break;
                case CardType.FourWithTwo: SpawnFourWithTwo(cards, result, lane, sourceBase); break;
                case CardType.Plane: SpawnPlane(cards, result, lane, sourceBase); break;
                case CardType.DoubleKingBomb: SpawnDoubleKingBomb(lane, sourceBase); break;
                case CardType.Single: SpawnSingles(cards, lane, sourceBase); break;
                default:
                    Debug.LogError($"[BattleManager] 未识别的牌型: {result.Type}，请检查 CardTypeDetector 和 DeployCards 是否同步");
                    break;
            }
        }

        // ─── 牌型生成 ──────────────────────────────────

        void SpawnBomb(Card[] cards, CardTypeResult result, Lane lane, Component sourceBase)
        {
            var pool = sourceBase?.GetComponent<SpawnPool>();
            var prefab = pool?.GetBombPrefab(result.MainRank);
            CreateUnitWithPrefab(prefab, result.MainRank, lane, sourceBase, CardType.Bomb);
        }

        void SpawnPair(Card[] cards, Lane lane, Component sourceBase)
        {
            if (cards.Length < 2) return;
            var u1 = CreateUnit(cards[0].Rank, lane, sourceBase, CardType.Pair);
            var u2 = CreateUnit(cards[0].Rank, lane, sourceBase, CardType.Pair);

            // 合击：对子生成的两个单位都提升攻击力（不再只给第二兵加额外伤害）
            if (_jointDamageBonus != 0f)
            {
                float atkMult = 1f + _jointDamageBonus;
                if (u1 != null)
                    u1.ApplyBuff(PairAtkBuffId, new CardUnit.StatBuff(atk: atkMult));
                if (u2 != null)
                    u2.ApplyBuff(PairAtkBuffId, new CardUnit.StatBuff(atk: atkMult));
            }
        }

        private const string PairAtkBuffId = "pair_atk";

        void SpawnTriple(Card[] cards, Lane lane, Component sourceBase)
        {
            if (cards.Length < 3) return;
            for (int i = 0; i < 3; i++)
            {
                var u = CreateUnit(cards[0].Rank, lane, sourceBase, CardType.Triple);
                if (u != null)
                {
                    if (i > 0) u.transform.position += new Vector3(0, (i - 1) * 0.4f, 0);
                    u.OnTakeDamageEvent += (damage, type) => RedistributeDamage(u, damage, type);
                }
            }
        }

        // 防止分担伤害递归调用的标志
        private bool _isRedistributingDamage;

        private void RedistributeDamage(CardUnit damaged, float damage, DamageType type)
        {
            if (_isRedistributingDamage) return;

            var allies = new List<CardUnit>();
            foreach (var unit in _allUnits)
            {
                if (unit == null || !unit.IsAlive || unit == damaged) continue;
                if (unit.IsLandlord != damaged.IsLandlord) continue;
                if (unit.SourceCardType != CardType.Triple) continue;
                // 统一口径：用边缘距离（碰撞体间隙）判定分担范围
                if (damaged.GetUnitEdgeDistance(unit) <= _shareRange) allies.Add(unit);
            }
            if (allies.Count < 2) return;

            _isRedistributingDamage = true;
            allies.Add(damaged);
            var sorted = allies.OrderByDescending(a => a.CurrentHP).ToList();

            // 主目标（最高血量）：设置分担伤害替代原始伤害
            sorted[0].ShareRedirected = true;
            sorted[0].SharedDamageOverride = damage * _shareMainPct;

            // 其他目标：直接施加分担伤害
            for (int i = 1; i < sorted.Count; i++)
                sorted[i].TakeDamage(damage * _shareOtherPct, type);

            _isRedistributingDamage = false;
        }

        void SpawnTripleWithOne(Card[] cards, CardTypeResult result, Lane lane, Component sourceBase)
        {
            for (int i = 0; i < 3; i++)
            {
                var u = CreateUnit(result.MainRank, lane, sourceBase, CardType.Triple);
                if (u != null) u.OnTakeDamageEvent += (damage, type) => RedistributeDamage(u, damage, type);
            }
            if (result.KickerRanks != null && result.KickerRanks.Length > 0)
            {
                var pool = sourceBase?.GetComponent<SpawnPool>();
                var prefab = pool?.GetBaitPrefab(result.KickerRanks[0]);
                CreateUnitWithPrefab(prefab, result.KickerRanks[0], lane, sourceBase);
            }
        }

        void SpawnTripleWithPair(Card[] cards, CardTypeResult result, Lane lane, Component sourceBase)
        {
            for (int i = 0; i < 3; i++)
            {
                var u = CreateUnit(result.MainRank, lane, sourceBase, CardType.Triple);
                if (u != null) u.OnTakeDamageEvent += (damage, type) => RedistributeDamage(u, damage, type);
            }
            if (result.KickerRanks != null && result.KickerRanks.Length > 0)
            {
                var pool = sourceBase?.GetComponent<SpawnPool>();
                var cavPrefab = pool?.GetCavalryPrefab(result.KickerRanks[0]);
                for (int i = 0; i < 2; i++)
                    CreateUnitWithPrefab(cavPrefab, result.KickerRanks[0], lane, sourceBase);
            }
        }

        void SpawnStraight(Card[] cards, Lane lane, Component sourceBase)
        {
            var units = new List<CardUnit>();
            CardType straightType = cards.Length >= 6 ? CardType.Straight6Plus : CardType.Straight;
            foreach (var card in cards)
            {
                var u = CreateUnit(card.Rank, lane, sourceBase, straightType);
                if (u != null)
                {
                    u.transform.position += new Vector3((Random.value - 0.5f) * 0.3f, 0, 0);
                    units.Add(u);
                }
            }

            if (units.Count >= 2)
            {
                bool is6Plus = cards.Length >= 6;
                float atkBoost = is6Plus ? _straight6SpeedBoost : _straightSpeedBoost;
                float moveBoost = is6Plus ? _straight6MoveSpeed : _straightMoveSpeed;
                foreach (var u in units)
                {
                    u.ApplyBuff("straight", new CardUnit.StatBuff(
                        atkInterval: 1f / atkBoost, moveSpeed: moveBoost));
                }
            }
        }

        void SpawnConsecutivePair(Card[] cards, CardTypeResult result, Lane lane, Component sourceBase)
        {
            var pool = sourceBase?.GetComponent<SpawnPool>();

            var seen = new HashSet<CardRank>();
            foreach (var card in cards)
            {
                if (!seen.Add(card.Rank)) continue;
                var prefab = pool?.GetConsecutivePairPrefab(card.Rank);
                CreateUnitWithPrefab(prefab, card.Rank, lane, sourceBase, CardType.ConsecutivePair);
            }
        }

        void SpawnFourWithTwo(Card[] cards, CardTypeResult result, Lane lane, Component sourceBase)
        {
            var pool = sourceBase?.GetComponent<SpawnPool>();
            var tPrefab = pool?.GetTankPrefab(result.MainRank);
            CreateUnitWithPrefab(tPrefab, result.MainRank, lane, sourceBase, CardType.FourWithTwo);

            if (result.KickerRanks != null)
            {
                foreach (var kr in result.KickerRanks)
                {
                    var dPrefab = pool?.GetDronePrefab(kr);
                    CreateUnitWithPrefab(dPrefab, kr, lane, sourceBase);
                }
            }
        }

        [Header("轰炸参数")]
        [Tooltip("轰炸持续时间（秒），轰炸机在此时间后自毁")]
        [SerializeField] private float _bombingDuration = 4f;
        [Tooltip("每次 tick 伤害倍率（实际伤害 = ATK × 此值），非真正每秒伤害")]
        [SerializeField] private float _bombingDamagePerSecond = 0.3f;
        [Tooltip("伤害间隔（秒），每过此时间对范围内敌人造成一次伤害")]
        [SerializeField] private float _bombingInterval = 0.8f;

        void SpawnPlane(Card[] cards, CardTypeResult result, Lane lane, Component sourceBase)
        {
            var pool = sourceBase?.GetComponent<SpawnPool>();

            // 每个连续三条的点数生成一个轰炸机
            int startRank = (int)result.MainRank - (result.Length - 1);
            for (int r = startRank; r <= (int)result.MainRank; r++)
            {
                var rank = (CardRank)r;
                var prefab = pool?.GetBomberPrefab(rank);
                var bomber = CreateUnitWithPrefab(prefab, rank, lane, sourceBase, CardType.Plane);
                if (bomber == null) continue;
                bomber.StartCoroutine(BombingRunCoroutine(bomber, sourceBase));
            }
        }

        private IEnumerator BombingRunCoroutine(CardUnit bomber, Component sourceBase)
        {
            float elapsed = 0f;
            Lane bomberLane = bomber.Lane;

            while (elapsed < _bombingDuration)
            {
                var filter = new ContactFilter2D().NoFilter();
                int count = Physics2D.OverlapCircle(bomber.VisualCenter, bomber.Stats.Range, filter, _overlapCache);
                for (int i = 0; i < count; i++)
                {
                    var enemy = _overlapCache[i].GetComponentInParent<CardUnit>();
                    if (enemy == null || !enemy.IsAlive || enemy.IsLandlord == bomber.IsLandlord) continue;
                    if (enemy.Lane != bomberLane) continue;
                    if (!bomber.CanAttackHeight(enemy.UnitHeight)) continue;
                    enemy.TakeDamage(bomber.Stats.ATK * _bombingDamagePerSecond, DamageType.Physical);
                }

                elapsed += _bombingInterval;
                yield return new WaitForSeconds(_bombingInterval);
            }

            if (bomber != null && bomber.IsAlive)
                bomber.TakeDamage(bomber.CurrentHP, DamageType.True);
        }

        void SpawnDoubleKingBomb(Lane lane, Component sourceBase)
        {
            var unit = SpawnHero(lane, sourceBase, awakened: true);
            if (unit != null) unit.transform.localScale = Vector3.one * 2f;
        }

        void SpawnJokerHero(Lane lane, Component sourceBase)
        {
            var unit = SpawnHero(lane, sourceBase, awakened: false);
            if (unit != null) unit.transform.localScale = Vector3.one * 1.3f;
        }

        void SpawnSingles(Card[] cards, Lane lane, Component sourceBase)
        {
            foreach (var card in cards)
            {
                if (card.IsJoker) { SpawnJokerHero(lane, sourceBase); continue; }
                CreateUnit(card.Rank, lane, sourceBase);
            }
        }

        // ─── 通用生成 ──────────────────────────────────

        CardUnit CreateUnit(CardRank rank, Lane lane, Component sourceBase, CardType cardType = CardType.Single)
        {
            var pool = sourceBase?.GetComponent<SpawnPool>();
            var prefab = pool?.GetPrefab(rank);
            if (prefab == null) { Debug.LogError($"[BattleManager] 未找到 Rank {rank} 的预制体映射"); return null; }
            return CreateUnitWithPrefab(prefab, rank, lane, sourceBase, cardType);
        }

        CardUnit CreateUnitWithPrefab(CardUnit prefab, CardRank rank, Lane lane, Component sourceBase, CardType cardType = CardType.Single)
        {
            if (prefab == null || sourceBase == null) return null;

            bool isLandlord = IsLandlord(sourceBase);
            Vector2 spawnPos = GetSpawnPosition(lane, sourceBase);
            var unit = unitFactory.Spawn(prefab, rank, lane, spawnPos, isLandlord);
            if (unit == null) return null;

            unit.SourceCardType = cardType;

            Debug.Log($"[SPAWN] {unit.name} isLandlord={isLandlord} pos={spawnPos} lane={lane}");

            RegisterUnit(unit);
            var rg = sourceBase.GetComponent<RouteGroup>();
            unit.FollowPath = rg?.CurrentRoute;
            SnapToPathStart(unit);
            unit.OnDied -= OnUnitDied; unit.OnDied += OnUnitDied;
            var enemies = GetEnemiesFor(unit);
            unit.SetEnemyUnits(enemies);
            unit.SetEnemyBuildings(_allBuildingTargets);
            Debug.Log($"[INIT] {unit.name} landlord={isLandlord} enemyCount={enemies.Count} route={unit.FollowPath?.name} pos={spawnPos}");

            if (_enableTyrantAura && isLandlord)
            {
                unit.ApplyBuff("tyrant", new CardUnit.StatBuff(
                    hp: _tyrantHpMultiplier, atk: _tyrantAtkMultiplier));
            }

            unit.SyncBaseScale();
            OnUnitSpawned?.Invoke(unit);

            return unit;
        }

        public CardUnit SpawnSummonedUnit(CardUnit prefab, Vector3 position, CardUnit summoner)
        {
            if (prefab == null || summoner == null) return null;
            if (unitFactory == null) return null;

            bool isLandlord = summoner.IsLandlord;
            Lane lane = summoner.Lane;

            var unit = unitFactory.Spawn(prefab, CardRank.Three, lane, position, isLandlord);
            if (unit == null) return null;

            unit.Summoner = summoner;

            RegisterUnit(unit);

            // 直接继承召唤师的路线，避免基地切换分路后召唤物走上错误的路
            unit.FollowPath = summoner.FollowPath;
            // 从召唤师位置开始，而非路径起点
            unit.SetPositionSynced(new Vector3(position.x, position.y, unit.transform.position.z));
            unit.OnDied -= OnUnitDied; unit.OnDied += OnUnitDied;
            unit.SetEnemyUnits(GetEnemiesFor(unit));

            unit.SyncBaseScale();
            OnUnitSpawned?.Invoke(unit);

            return unit;
        }

        private Vector2 GetSpawnPosition(Lane lane, Component sourceBase)
        {
            var pool = sourceBase?.GetComponent<SpawnPool>();
            if (pool != null && pool.SpawnPoint != null) return pool.SpawnPoint.position;
            if (sourceBase != null) return sourceBase.transform.position;
            return MapController.GetSpawnPosition(lane, false);
        }

        private void SnapToPathStart(CardUnit unit)
        {
            if (unit.FollowPath != null && unit.FollowPath.waypoints.Length > 0 && unit.FollowPath.waypoints[0] != null)
            {
                Vector3 pathStart = unit.FollowPath.GetPoint(0);
                unit.SetPositionSynced(new Vector3(pathStart.x, pathStart.y, unit.transform.position.z));
            }
        }
    }
}
