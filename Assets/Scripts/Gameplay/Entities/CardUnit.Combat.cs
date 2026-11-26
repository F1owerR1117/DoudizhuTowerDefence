using System.Collections;
using System.Collections.Generic;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Gameplay.Battle;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// CardUnit 战斗模块（partial class）。
    /// 负责索敌、攻击、伤害计算、嘲讽检测等战斗相关逻辑。
    /// Phase 2：Fusion 模式下通过 SimulationDisabled 禁用旧战斗逻辑。
    /// </summary>
    public partial class CardUnit
    {
        /// <summary>
        /// 禁用旧战斗逻辑（Fusion 模式下设为 true）。
        /// 旧方法保留但跳过执行，由 CombatSystem 驱动战斗。
        /// </summary>
        [System.NonSerialized] public bool SimulationDisabled;

        // ─── 索敌 ─────────────────────────────────────

        protected virtual void UpdateTarget()
        {
            if (SimulationDisabled) return;
            // 目标已死亡 → 重新索敌
            if (Target == null || !Target.IsAlive)
            {
                var old = Target;
                Target = FindNearestEnemy();
                return;
            }

            // 目标超出追击范围 → 放弃目标
            if (!IsTargetInRange(Target))
            {
                float edgeDist = GetUnitEdgeDistance(Target);
                if (edgeDist > Stats.Range * 3f)
                {
                    Target = FindNearestEnemy();
                }
            }
        }

        /// <summary>由外部组件覆盖索敌逻辑（如骑兵追远程）</summary>
        public System.Func<CardUnit> OverrideFindTarget;

        /// <summary>由外部组件覆盖攻击距离判定（如点杀扩展范围）。返回对指定目标的有效攻击距离。</summary>
        public System.Func<CardUnit, float> OverrideAttackRange;

        /// <summary>
        /// 找到距离本单位最近的敌方嘲讽光环携带者。
        /// 嘲讽生效前提：目标必须同时落在「光环半径」与「我方攻击范围」的交集内。
        /// 多嘲讽冲突时，优先攻击离自己最近的那个。
        /// </summary>
        private readonly Collider2D[] _tauntBuffer = new Collider2D[64];
        private static readonly ContactFilter2D _tauntFilter = new ContactFilter2D { useTriggers = true, useLayerMask = false };

        private CardUnit FindNearestTauntSourceFor(CardUnit self)
        {
            CardUnit best = null;
            float bestDist = float.MaxValue;
            // 扫描范围取所有嘲讽源中最大的光环半径，保证不漏检
            float scanRadius = 0f;
            if (_enemyUnits != null)
            {
                foreach (var e in _enemyUnits)
                    if (e != null && e.IsAlive && e.IsTauntSource && e.TauntRadius > scanRadius)
                        scanRadius = e.TauntRadius;
            }
            if (scanRadius <= 0f) return null;

            int count = Physics2D.OverlapCircle(self.VisualCenter, scanRadius, _tauntFilter, _tauntBuffer);
            for (int i = 0; i < count; i++)
            {
                var unit = _tauntBuffer[i].GetComponentInParent<CardUnit>();
                if (unit == null || !unit.IsAlive || !unit.IsTauntSource) continue;
                if (unit.IsLandlord == self.IsLandlord) continue;
                if (!self.CanAttackHeight(unit.UnitHeight)) continue;

                // 条件1：自己在嘲讽源的光环半径内
                float auraDist = self.GetUnitEdgeDistance(unit);
                if (auraDist > unit.TauntRadius) continue;
                // 条件2：嘲讽源在自己的攻击范围内
                if (!self.IsTargetInRange(unit)) continue;

                if (auraDist < bestDist)
                {
                    bestDist = auraDist;
                    best = unit;
                }
            }
            return best;
        }

        protected virtual CardUnit FindNearestEnemy()
        {
            // 1. 嘲讽优先级最高（仅在攻击范围内生效）
            var tauntTarget = FindNearestTauntSourceFor(this);
            if (tauntTarget != null)
                return tauntTarget;

            // 2. 兵种特化索敌
            if (OverrideFindTarget != null) return OverrideFindTarget();

            // 3. 默认索敌：用边缘距离替代中心距，大单位不会被漏检
            float detectRange = DetectionRange;
            CardUnit bestSameLane = null;
            float minSame = float.MaxValue;
            CardUnit bestOther = null;
            float minOther = float.MaxValue;

            if (_enemyUnits == null) return null;
            foreach (var enemy in _enemyUnits)
            {
                if (enemy == null || !enemy.IsAlive || enemy._isBuilding || enemy == this) continue;
                if (!CanAttackHeight(enemy.UnitHeight)) continue;

                float edgeDist = GetUnitEdgeDistance(enemy);

                if (edgeDist > detectRange) continue;
                if (enemy._lane == _lane && edgeDist < minSame)
                {
                    minSame = edgeDist;
                    bestSameLane = enemy;
                }
                else if (edgeDist < minOther)
                {
                    minOther = edgeDist;
                    bestOther = enemy;
                }
            }

            var result = bestSameLane ?? bestOther;
            return result;
        }

        /// <summary>
        /// 动态检测范围内最近的敌方建筑。
        /// 兵种沿路线行进时，自动发现沿途的敌方建筑。
        /// </summary>
        private IBuildingTarget FindNearestEnemyBuilding()
        {
            if (_enemyBuildings == null) return null;

            IBuildingTarget best = null;
            float bestDist = float.MaxValue;
            float detectRange = DetectionRange;

            foreach (var target in _enemyBuildings)
            {
                if (target == null || target.IsDestroyed) continue;
                var cu = target as CardUnit;
                if (cu == null) continue;
                if (cu.IsLandlord == IsLandlord) continue;
                if (_lane != Lane.None && cu.Lane != Lane.None && cu.Lane != _lane) continue;

                float dist = GetEdgeDistance(target);
                if (dist <= detectRange && dist < bestDist)
                {
                    bestDist = dist;
                    best = target;
                }
            }
            return best;
        }

        /// <summary>
        /// 多目标索敌：返回范围内最多 _maxTargets 个敌方单位，按距离排序。
        /// 嘲讽目标始终排在最前。
        /// </summary>
        private List<CardUnit> FindAllTargets()
        {
            var result = new List<CardUnit>();
            if (_maxTargets <= 1) return result;

            float searchRadius = _multiTargetRadius > 0f ? _multiTargetRadius : Stats.Range;

            // 嘲讽目标优先
            var taunt = FindNearestTauntSourceFor(this);
            if (taunt != null && IsTargetInRange(taunt))
                result.Add(taunt);

            // 收集范围内所有敌人
            if (_enemyUnits != null)
            {
                foreach (var enemy in _enemyUnits)
                {
                    if (result.Count >= _maxTargets) break;
                    if (enemy == null || !enemy.IsAlive || enemy._isBuilding || enemy == this) continue;
                    if (result.Contains(enemy)) continue;
                    if (!CanAttackHeight(enemy.UnitHeight)) continue;
                    if (GetUnitEdgeDistance(enemy) > searchRadius) continue;
                    result.Add(enemy);
                }
            }

            // 按距离排序（嘲讽已在第一位，其余按距离）
            if (result.Count > 1)
            {
                result.Sort((a, b) =>
                {
                    if (a == taunt) return -1;
                    if (b == taunt) return 1;
                    return GetUnitEdgeDistance(a).CompareTo(GetUnitEdgeDistance(b));
                });
            }

            if (result.Count > _maxTargets)
                result.RemoveRange(_maxTargets, result.Count - _maxTargets);

            return result;
        }

        /// <summary>
        /// 对指定目标的「有效攻击距离」：默认是 Stats.Range，
        /// 若外部接管了攻击范围（如点杀对残血目标的越界攻击），则使用接管值。
        /// 飞扑的触发/落点也按这个距离计算，因此会被点杀之类的索敌改动影响。
        /// </summary>
        public float GetEffectiveRange(CardUnit target)
        {
            if (target == null) return Stats.Range;
            return OverrideAttackRange != null ? OverrideAttackRange(target) : Stats.Range;
        }

        protected bool IsTargetInRange(CardUnit target)
        {
            if (target == null) return false;
            if (!CanAttackHeight(target.UnitHeight)) return false;
            return GetUnitEdgeDistance(target) <= GetEffectiveRange(target);
        }

        /// <summary>
        /// 是否是「治疗攻击」的目标：开启治疗被动、且目标是同阵营友军（含自己）。
        /// 这类目标不走伤害结算，攻击帧改为施加治疗。
        /// </summary>
        public bool IsHealingTarget(CardUnit target)
        {
            if (target == null) return false;
            var p = Passives;
            if (p == null || !p.enableHeal) return false;
            return target.IsLandlord == IsLandlord;
        }

        // ─── 攻击 ─────────────────────────────────────

        /// <summary>每次攻击时的额外伤害累积（供被动技能如人海连击使用）</summary>
        [System.NonSerialized] public float _bonusDamage;

        /// <summary>
        /// 开始攻击流程：设置动画速度 → 播放攻击动画 → 记录目标。
        /// 实际伤害由动画打击帧的 OnAttackHitFrame() 触发。
        /// </summary>
        protected virtual void TryAttack(CardUnit target)
        {
            if (_isAttacking) return;
            if (target == null) return;
            // 允许「治疗自己」：仅当目标是自身且属于治疗攻击时才放行
            if (target == this && !IsHealingTarget(target)) return;

            // 统一门禁：合法性检查（含 BOSS 激活状态）；治疗友军时跳过敌我判定
            var bm = BattleManager.Instance;
            if (bm != null && !IsHealingTarget(target) && !bm.IsValidCombatTarget(this, target)) return;

            Debug.Log($"[TRY_ATTACK] {name} target={target.name} dist={GetUnitEdgeDistance(target):F1} isLandlord={IsLandlord} targetIsLandlord={target.IsLandlord}");
            _isAttacking = true;
            _attackTarget = target;
            _hitCountDealt = 0;
            _animDone = false;
            _hitTimelineDone = false;
            _nextHitIndex = 0;
            _attackTimer = 0f;
            _attackStateTimer = 0f;

            // 创建攻击 Timeline（时间驱动攻击帧）
            float interval = Stats.AttackInterval;
            int hitCount = Mathf.Max(1, Stats.HitCount);
            _hitTimes = new float[hitCount];
            for (int i = 0; i < hitCount; i++)
                _hitTimes[i] = (float)(i + 1) / hitCount;

            // 多目标单位：攻击开始时锁定目标快照
            if (_maxTargets > 1)
                _attackSnapshotTargets = FindAllTargets();

            // 播放动画（纯表现）
            float clipLen = GetAttackClipLength();
            float speed = clipLen > 0f ? Mathf.Min(clipLen / interval, 4f) : 1f;
            SetAnimSpeed(speed);
            UpdateAnimatorState(2);

            // Master 广播攻击事件（Client 播放攻击动画）
            BroadcastAttack(target);
        }

        /// <summary>广播攻击事件（仅 Master 调用）</summary>
        private void BroadcastAttack(CardUnit target)
        {
            if (!SimulatesCombat) return;
        }

        /// <summary>广播受击事件（仅 Master 调用）</summary>
        private void BroadcastHit(float damage, DamageType type)
        {
            if (!SimulatesCombat) return;
        }

        /// <summary>
        /// 计算本次攻击伤害（含被动加成），消耗 _bonusDamage。
        /// Animation Event 和协程共用，确保伤害一致。
        /// </summary>
        private float ComputeAndConsumePassiveDamage()
        {
            _bonusDamage = 0f;
            OnAttackEvent?.Invoke(_attackTarget);
            OnAttack(_attackTarget);
            float total = CurrentATK + _bonusDamage;
            return total;
        }

        /// <summary>
        /// 生成弹丸并发射。Animation Event 和协程兜底共用。
        /// </summary>
        private void SpawnProjectile(float damage)
        {
            Vector3 spawnPos = _firePoint != null ? _firePoint.position : transform.position;

            // 有明确的敌方单位目标 → 打单位
            if (_attackTarget != null && _attackTarget.IsAlive && _attackTarget != this)
            {
                if (_attackTarget.IsLandlord == IsLandlord)
                {
                    _hitCountDealt = Stats.HitCount;
                    return;
                }

                var proj = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
                proj.Fire(this, _attackTarget, damage, DamageType.Physical);
            }
            // 无单位目标（正在攻击建筑）→ 打建筑
            else if (_attackTarget == null && CurrentTarget != null && !CurrentTarget.IsDestroyed)
            {
                var proj = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
                proj.FireAtBuilding(this, CurrentTarget, damage);
            }
            // 目标已死 → 不发射子弹
        }

        /// <summary>
        /// 攻击帧伤害逻辑（纯伤害执行）。
        /// 由 Animation Event → AttackEventRelay → OnAttackHitFrame 触发。
        /// </summary>
        public void OnAttackHitFrame()
        {
            if (_isDying) return;
            if (!_isAttacking) return;

            // 门禁：未激活 BOSS 不允许造成伤害
            var boss = GetComponent<BossController>();
            if (boss != null && !boss.IsActive) return;

            // ── 治疗攻击：目标为友军时只施加治疗，不结算伤害、不触发攻击类被动 ──
            if (IsHealingTarget(_attackTarget))
            {
                var healCtl = Passives;
                if (healCtl != null)
                {
                    if (_maxTargets > 1)
                    {
                        var healList = new List<CardUnit>();
                        healCtl.CollectHealTargets(healList, _maxTargets);
                        if (healList.Count == 0) healList.Add(_attackTarget);
                        healCtl.ExecuteHealTargets(healList);
                    }
                    else
                    {
                        healCtl.ExecuteHealTarget(_attackTarget);
                    }
                }
                _hitCountDealt++;
                return;
            }

            // 多目标模式
            if (_maxTargets > 1)
            {
                var targets = _attackSnapshotTargets ?? FindAllTargets();

                // 建筑攻击：把 CurrentTarget（建筑）也加入目标列表
                if (_attackTarget == null && CurrentTarget != null && !CurrentTarget.IsDestroyed)
                {
                    var bldgCU = CurrentTarget as CardUnit;
                    if (bldgCU != null && bldgCU.IsLandlord != IsLandlord && GetEdgeDistance(CurrentTarget) <= Stats.Range)
                    {
                        if (targets.Count < _maxTargets)
                            targets.Add(bldgCU);
                    }
                }

                if (targets.Count == 0) { _hitCountDealt++; return; }

                // 在触发被动之前锁定「本次可打的目标」：
                // 冲锋一击退出时会还原可攻击高度，若在 OnAttackEvent 之后才过滤，冲锋首击会漏掉空中目标。
                var hittable = new List<CardUnit>(targets.Count);
                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    if (t != null && t.IsAlive && CanAttackHeight(t.UnitHeight)) hittable.Add(t);
                }
                if (hittable.Count == 0) { _hitCountDealt++; return; }

                // 一次性计算基础伤害 + 被动加成（人海/冲锋等）
                _bonusDamage = 0f;
                OnAttackEvent?.Invoke(_attackTarget);
                OnAttack(_attackTarget);
                float totalDmg = CurrentATK + _bonusDamage;

                // 对每个目标独立触发 per-target 被动（溅射/眩晕等）+ 造成伤害
                foreach (var t in hittable)
                {
                    if (t == null || !t.IsAlive) continue;

                    // 触发每个目标的溅射/眩晕等被动（不重复人海/冲锋）
                    OnPerTargetAttackEvent?.Invoke(t);

                    if (_isRanged && _projectilePrefab != null)
                    {
                        var spawnPos = _firePoint != null ? _firePoint.position : transform.position;
                        var proj = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
                        proj.Fire(this, t, totalDmg, DamageType.Physical);
                    }
                    else
                    {
                        t.LastAttacker = Summoner != null ? Summoner : this;
                        t.TakeDamage(totalDmg, DamageType.Physical, Summoner != null ? Summoner : this);
                    }
                }
            }
            // 单目标模式
            else
            {
                if (_isRanged && _projectilePrefab != null)
                {
                    float totalDmg = ComputeAndConsumePassiveDamage();
                    SpawnProjectile(totalDmg);
                }
                else
                {
                    DealAttackDamage();
                }
            }

            _hitCountDealt++;
        }

        /// <summary>
        /// 执行近程攻击伤害。由 OnAttackHitFrame 调用。
        /// </summary>
        private void DealAttackDamage()
        {
            // 必须在触发被动之前锁定可命中性：
            // 冲锋一击的退出（ApplyCharge → ExitChargeState）会还原「可攻击高度」，
            // 若在此之后再判断 CanAttackHeight，本次冲锋首击会打空（例如冲锋期间可打空中，退出后判定为不可打）。
            bool canHitTarget = _attackTarget != null && _attackTarget.IsAlive && _attackTarget != this
                                && CanAttackHeight(_attackTarget.UnitHeight);

            float totalDmg = ComputeAndConsumePassiveDamage();

            // 有明确的敌方单位目标 → 打单位
            if (canHitTarget)
            {
                _attackTarget.LastAttacker = Summoner != null ? Summoner : this;
                _attackTarget.TakeDamage(totalDmg, DamageType.Physical, Summoner != null ? Summoner : this);
            }
            // 无单位目标（正在攻击建筑）→ 打建筑（需在攻击范围内）
            else if (_attackTarget == null && CurrentTarget != null && !CurrentTarget.IsDestroyed)
            {
                // 伤害门禁：防止攻击友方建筑（Animator 残留事件等绕过 AI 决策的情况）
                if (CurrentTarget is CardUnit targetCU && targetCU.IsLandlord == IsLandlord)
                {
                    CurrentTarget = null;
                    return;
                }
                if (GetEdgeDistance(CurrentTarget) <= Stats.Range)
                {
                    CurrentTarget.TakeDamage(totalDmg);
                }
            }
            // 目标已死或不在范围内 → 不造成伤害
        }

        /// <summary>
        /// 攻击 Timeline 更新：时间驱动攻击帧，替代 Animation Event + HitFrameCoroutine。
        /// 由 OnUpdate() 每帧调用。
        /// </summary>
        private void UpdateAttackTimeline()
        {
            if (_hitTimes == null || _hitTimes.Length == 0) { _hitTimelineDone = true; return; }
            if (Stats.HP <= 0f) { InterruptAttack(); return; }

            // 门禁：未激活 BOSS 停止 Timeline
            var boss = GetComponent<BossController>();
            if (boss != null && !boss.IsActive)
            {
                InterruptAttack();
                return;
            }

            // InterruptAttack 可能已清空 _hitTimes
            if (_hitTimes == null) return;

            float interval = Stats.AttackInterval;
            _attackTimer += Time.deltaTime;
            float t = interval > 0f ? _attackTimer / interval : 1f;

            // 触发所有已到达的攻击帧（每次循环都重新判空：ExecuteHit 可能因目标失效调用 InterruptAttack 把 _hitTimes 置空）
            while (_hitTimes != null && _nextHitIndex < _hitTimes.Length && t >= _hitTimes[_nextHitIndex])
            {
                ExecuteHit(_nextHitIndex);
                _nextHitIndex++;
            }

            // Timeline 完成
            if (_hitTimes != null && _nextHitIndex >= _hitTimes.Length)
                _hitTimelineDone = true;
        }

        /// <summary>
        /// 执行单次攻击帧：授权验证。
        /// 由 UpdateAttackTimeline 调用，伤害由 Animation Event 的 OnAttackHitFrame 触发。
        /// </summary>
        private void ExecuteHit(int hitIndex)
        {
            if (_isDying || !_isAttacking) return;

            // 战斗系统授权验证
            var bm = BattleManager.Instance;
            if (bm != null)
            {
                var ownerBoss = GetComponent<BossController>();
                if (ownerBoss != null && !ownerBoss.IsActive) return;
                if (_attackTarget != null && !IsHealingTarget(_attackTarget) && !bm.IsValidCombatTarget(this, _attackTarget))
                {
                    InterruptAttack();
                    return;
                }
            }
        }

        /// <summary>攻击事件（供外部组件监听，每次攻击触发一次）</summary>
        public event System.Action<CardUnit> OnAttackEvent;
        /// <summary>多目标攻击事件（每个目标独立触发，用于溅射/眩晕等按目标生效的被动）</summary>
        public event System.Action<CardUnit> OnPerTargetAttackEvent;
        /// <summary>受伤事件（参数：伤害值, 伤害类型）。用于音效和分担，不含撕裂加成。</summary>
        public event System.Action<float, DamageType> OnTakeDamageEvent;
        /// <summary>伤害结算完成事件（参数：最终伤害值, 伤害类型）。含撕裂加成，供飘字使用。</summary>
        public event System.Action<float, DamageType> OnDamageCalculated;
        /// <summary>死亡事件</summary>
        public event System.Action OnDeathEvent;

        protected virtual void OnAttack(CardUnit target) { }

        // ─── 批量伤害结算 ─────────────────────────────

        private static bool _batchDamageEnabled;

        /// <summary>启用/禁用批量伤害结算模式</summary>
        public static void SetBatchDamageEnabled(bool enabled) => _batchDamageEnabled = enabled;

        // ─── 受伤害 ───────────────────────────────────

        public virtual void TakeDamage(float rawDamage, DamageType type)
        {
            TakeDamage(rawDamage, type, null);
        }

        /// <summary>
        /// 受伤害（带来源）。source 用于「处决」等需要知道攻击者的被动；
        /// 火焰区域、死亡爆炸等无主伤害传 null 即可。
        /// </summary>
        public virtual void TakeDamage(float rawDamage, DamageType type, CardUnit source)
        {
            if (SimulationDisabled) return;
            if (!SimulatesCombat) return; // Client 不处理伤害
            if (!IsAlive) return;
            if (Invulnerable) return;

            float hpBefore = _currentHP;

            // 真实伤害：无视屏障、盾墙减免、伤害减免
            if (type == DamageType.True)
            {
                if (_batchDamageEnabled)
                {
                    DoudizhuTower.Gameplay.Battle.DamageQueue.Enqueue(this, rawDamage, source);
                    return;
                }
                _currentHP -= rawDamage;
                OnHPChanged?.Invoke(_unitId, _currentHP);

                // 真实掉血 → 刷新流血时长
                if (_currentHP < hpBefore) RefreshBleedOnDamaged();

                // Master 广播受击事件（Client 播放受击动画+飘字）
                BroadcastHit(rawDamage, type);

                if (_currentHP <= 0f) { _currentHP = 0f; Die(); return; }
                TryExecuteBy(source, hpBefore);
                return;
            }

            // 屏障层消耗：每层吸收一次攻击的全部伤害
            if (ShieldBlocks > 0)
            {
                ShieldBlocks--;
                return;
            }

            rawDamage = UnitPassives.ApplyShieldWallGlobal(this, rawDamage, type);

            // 伤害减免（重骑兵/铁骑兵冲锋减伤）
            if (DamageReduction > 0f) rawDamage *= (1f - DamageReduction);

            // 伤害吸收（诱饵护盾/帝王盾）
            if (DamageAbsorbRemaining > 0f)
            {
                float absorbed = Mathf.Min(rawDamage, DamageAbsorbRemaining);
                rawDamage -= absorbed;
                DamageAbsorbRemaining -= absorbed;
                if (rawDamage <= 0f) return;
            }

            // 受击音效（撕裂乘算前，使用原始伤害）
            OnTakeDamageEvent?.Invoke(rawDamage, type);

            // 分担伤害：用分担值替代原始伤害
            if (ShareRedirected)
            {
                ShareRedirected = false;
                rawDamage = SharedDamageOverride;
                SharedDamageOverride = 0f;
                if (rawDamage <= 0f) return;
            }

            // B3: 结算撕裂易伤叠加——每层 +5% 受伤
            float tearMultiplier = UnitPassives.GetTearMultiplier(this);
            float finalDamage = rawDamage * tearMultiplier;

            // 飘字显示实际伤害（含撕裂加成）
            OnDamageCalculated?.Invoke(finalDamage, type);

            // 批量模式：入队，帧末统一结算 HP 和死亡
            if (_batchDamageEnabled)
            {
                DoudizhuTower.Gameplay.Battle.DamageQueue.Enqueue(this, finalDamage, source);
                return;
            }

            _currentHP -= finalDamage;
            OnHPChanged?.Invoke(_unitId, _currentHP);

            // 真实掉血 → 刷新流血时长
            if (_currentHP < hpBefore) RefreshBleedOnDamaged();

            // Master 广播受击事件（Client 播放受击动画+飘字）
            BroadcastHit(finalDamage, type);

            if (_currentHP <= 0f)
            {
                _currentHP = 0f;
                Die();
                return;
            }

            // 处决：本次伤害真实掉血后仍存活，但血量低于处决阈值
            TryExecuteBy(source, hpBefore);
        }

        /// <summary>
        /// 处决判定：本次伤害必须真实扣到 HP（全被护盾/格挡吃掉的伤害不触发），
        /// 且结算后血量严格低于攻击者的处决阈值，则直接死亡。
        /// </summary>
        private void TryExecuteBy(CardUnit source, float hpBefore)
        {
            if (source == null) return;
            if (!IsAlive) return;
            if (_isBuilding) return;                 // 建筑不吃处决（防止一刀秒基地）
            if (_currentHP >= hpBefore) return;      // 本次没有真实掉血（护盾全吸收）

            var passives = source.Passives;
            if (passives == null || !passives.enableExecute) return;

            float threshold = passives.executeThreshold;
            if (threshold <= 0f) return;
            if (_currentHP >= threshold) return;     // 未低于阈值

            _currentHP = 0f;
            OnHPChanged?.Invoke(_unitId, _currentHP);
            OnExecuted?.Invoke();                    // 飘字：黄色「处决」

            // 表现：由处决执行者播放特效/音效
            var execVfx = source.GetComponent<UnitVFX>();
            var execAudio = source.GetComponent<UnitAudio>();
            execVfx?.PlayExecute(transform);
            execAudio?.PlayExecute();

            Die();
        }

        /// <summary>
        /// 批量结算阶段调用：执行实际 HP 扣除和死亡判定。
        /// 由 DamageQueue.ProcessAll() 调用，不应直接调用。
        /// </summary>
        public void ApplyDamage(float finalDamage, CardUnit source = null)
        {
            if (!IsAlive) return;
            if (Invulnerable) return;

            float hpBefore = _currentHP;
            _currentHP -= finalDamage;
            OnHPChanged?.Invoke(_unitId, _currentHP);

            // 真实掉血 → 刷新流血时长
            if (_currentHP < hpBefore) RefreshBleedOnDamaged();

            // Master 广播受击事件（Client 播放受击动画+飘字）
            BroadcastHit(finalDamage, DamageType.Physical);

            if (_currentHP <= 0f)
            {
                _currentHP = 0f;
                Die();
                return;
            }

            TryExecuteBy(source, hpBefore);
        }

        // ─── 死亡 ─────────────────────────────────────

        public virtual void Die()
        {
            if (SimulationDisabled) return;
            if (!SimulatesCombat) return; // Client 不触发死亡，由 Master 广播驱动
            _isDying = true;

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
            SetAnimSpeed(1f);

            // 清除召唤帧事件，防止死亡后仍触发生效
            OnSummonFrame = null;

            // UnitAudio 会监听 OnDeathEvent 来播放死亡音效
            OnDeathEvent?.Invoke();

            // 通知击杀者（供召唤师等被动使用）
            if (LastAttacker != null && LastAttacker.IsAlive)
                LastAttacker.OnKillEvent?.Invoke(this);

            if (_isBuilding || _isBoss)
                OnDestroyed?.Invoke(this);

            // 播放死亡动画，动画播完后触发 OnDied（回收到对象池）
            StartCoroutine(PlayDeathAnimCoroutine(() =>
            {
                OnDied?.Invoke(_unitId);
            }));
        }

        /// <summary>
        /// Client 端视觉死亡（仅播动画+回收，不触发战斗死亡管线）。
        /// 由 Master 广播 UNIT_DIED 驱动。
        /// </summary>
        public void VisualDeath()
        {
            if (!IsAlive || _isDying) return;
            _isDying = true;
            _currentHP = 0f;
            _isAttacking = false;
            SetAnimSpeed(1f);
            StartCoroutine(PlayDeathAnimCoroutine(() =>
            {
                OnDied?.Invoke(_unitId);
            }));
        }

        // ─── 网络同步专用 ───────────────────────────

        /// <summary>设置 HP 值（网络校正用，不触发伤害流程）</summary>
        public void SetHP(float hp)
        {
            if (_isDying) return;
            _currentHP = Mathf.Clamp(hp, 0f, Stats.HP);
            OnHPChanged?.Invoke(_unitId, _currentHP);
        }

        /// <summary>强制死亡（网络校正用，跳过伤害流程直接触发死亡）</summary>
        public void ForceDie()
        {
            if (_isDying || !IsAlive) return;
            _currentHP = 0f;
            OnHPChanged?.Invoke(_unitId, _currentHP);
            Die();
        }
    }
}
