using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// CardUnit 动画模块（partial class）。
    /// 负责动画状态机控制、对象池管理、Gizmos 可视化。
    /// </summary>
    public partial class CardUnit
    {
        // ─── 动画驱动攻击状态 ─────────────────────────────
        private int _currentAnimState = -1;

        /// <summary>
        /// 更新动画状态（由子类 Update 循环调用，或网络同步调用）
        /// 状态值：0=Idle, 1=Walk, 2=Attack
        /// </summary>
        public void UpdateAnimatorState(int state)
        {
            if (_isDying) return; // 死亡动画期间禁止一切状态切换
            if (state == _currentAnimState) return; // 状态未变，不重复设置
            _currentAnimState = state;

            // 优先使用 SimpleAnimator，否则使用 Animator
            if (_simpleAnimator != null)
            {
                // SimpleAnimator 会自动处理动画切换
                // 这里只需要确保 Animator 参数被设置
                var anim = _simpleAnimator.Animator;
                if (anim != null && anim.isActiveAndEnabled)
                {
                    anim.SetInteger("State", state);
                }
                else
                {
                    // SimpleAnimator 的 Animator 未就绪，尝试使用 CardUnit 的 _animator
                    if (_animator != null && _animator.isActiveAndEnabled)
                        _animator.SetInteger("State", state);
                }
            }
            else if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetInteger("State", state);
            }
            else
            {
                // 尝试重新获取 Animator
                _animator = GetComponentInChildren<Animator>(true);
                if (_animator != null && _animator.isActiveAndEnabled)
                    _animator.SetInteger("State", state);
            }
        }

        /// <summary>触发一次性动画（冲锋、震波、溅射等）</summary>
        public void TriggerAnim(string name)
        {
            if (_isDying && name != "Death" && name != "DeathExplosion" && name != "Burn") return;

            // 飞扑期间屏蔽其他一次性动画（死亡与飞扑本身除外），避免"边飞边播眩晕/震波"
            if (IsPouncing && name != "Death" && name != "Pounce")
            {
                var pounceCtl = Passives;
                if (pounceCtl != null && pounceCtl.enablePounce && pounceCtl.pounceBlockOneShotAnim) return;
            }

            if (_simpleAnimator != null)
                _simpleAnimator.Trigger(name);
            else if (_animator != null && _animator.isActiveAndEnabled)
                _animator.SetTrigger(name);
        }

        /// <summary>
        /// Animation Event 回调：召唤帧触发。
        /// 由 UnitPassives 注册，在召唤动画的指定帧调用。
        /// </summary>
        public event System.Action OnSummonFrame;

        /// <summary>
        /// Animation Event 入口：召唤帧触发。
        /// 在召唤动画的指定帧添加 Animation Event，Function 填 "OnSummonFrame"。
        /// </summary>
        public void OnSummonFrameEvent() => OnSummonFrame?.Invoke();

        /// <summary>
        /// 播放死亡动画，动画播完后回调 onComplete。
        /// 如果未配置死亡动画，立即回调。
        /// </summary>
        public System.Collections.IEnumerator PlayDeathAnimCoroutine(System.Action onComplete)
        {
            // 只在配置了死亡动画时才触发 Death，避免与 DeathExplosion/Burn 竞争
            bool hasDeathClip = _simpleAnimator != null && _simpleAnimator.deathClip != null;
            if (hasDeathClip)
                TriggerAnim("Death");

            float duration = hasDeathClip ? _simpleAnimator.deathClip.length : 0f;

            yield return new WaitForSeconds(duration);
            onComplete?.Invoke();
        }

        /// <summary>设置持续动画状态（减速光环、盾墙等开关型效果）</summary>
        public void SetAnimBool(string name, bool value)
        {
            if (_simpleAnimator != null)
                _simpleAnimator.SetBool(name, value);
            else if (_animator != null && _animator.isActiveAndEnabled)
                _animator.SetBool(name, value);
        }

        // ─── 动画驱动攻击 ──────────────────────────

        /// <summary>获取当前 Animator 组件引用</summary>
        private Animator GetAnimator()
        {
            if (_simpleAnimator != null)
            {
                var anim = _simpleAnimator.Animator;
                if (anim != null && anim.isActiveAndEnabled) return anim;
            }
            if (_animator != null && _animator.isActiveAndEnabled) return _animator;
            _animator = GetComponentInChildren<Animator>(true);
            return _animator;
        }

        /// <summary>设置动画播放速度（1=正常，>1=加速，<1=减速）</summary>
        public void SetAnimSpeed(float speed)
        {
            var anim = GetAnimator();
            if (anim != null) anim.speed = speed;
        }

        /// <summary>获取攻击动画剪辑时长（秒），无法获取时返回 0</summary>
        private float GetAttackClipLength()
        {
            if (_simpleAnimator != null && _simpleAnimator.attackClip != null)
                return _simpleAnimator.attackClip.length;
            return 0f;
        }

        /// <summary>获取攻击动画打击帧的 normalizedTime（从 Animation Event 读取，缓存）</summary>
        private float GetHitNormalizedTime()
        {
            if (_cachedHitNormalizedTime > 0f) return _cachedHitNormalizedTime;
            if (_simpleAnimator != null && _simpleAnimator.attackClip != null)
            {
                var clip = _simpleAnimator.attackClip;
                foreach (var evt in clip.events)
                {
                    if (evt.functionName == "OnAttackHitFrame")
                    {
                        _cachedHitNormalizedTime = clip.length > 0f ? evt.time / clip.length : 0.3f;
                        return _cachedHitNormalizedTime;
                    }
                }
            }
            _cachedHitNormalizedTime = 0.3f;
            return _cachedHitNormalizedTime;
        }

        /// <summary>攻击动画是否已播放完毕（normalizedTime >= 1）</summary>
        private bool IsAttackAnimDone()
        {
            var anim = GetAnimator();
            if (anim == null) return true;
            var info = anim.GetCurrentAnimatorStateInfo(0);
            return info.normalizedTime >= 1f;
        }

        // ─── 视野裁剪：摄像机外禁用动画 ─────────
        private void OnBecameVisible()
        {
            if (_simpleAnimator != null) _simpleAnimator.enabled = true;
            else if (_animator != null) _animator.enabled = true;
        }
        private void OnBecameInvisible()
        {
            if (_simpleAnimator != null) _simpleAnimator.enabled = false;
            else if (_animator != null) _animator.enabled = false;
        }

        // ─── 对象池 ───────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!GizmoDraw.ShouldDraw(gameObject)) return;

            // 碰撞箱轮廓（青色）+ 攻击范围（红色）
            // 统一使用 bounds（世界空间），与实际碰撞检测一致
            Collider2D col = _collider != null ? _collider : GetComponentInChildren<Collider2D>();
            if (col == null) return;

            Bounds bounds = col.bounds;
            Vector3 center = bounds.center;
            Vector3 worldSize = bounds.size;

            // 碰撞箱轮廓（世界空间尺寸）
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireCube(center, worldSize);

            // 攻击范围 = 碰撞体外形向外膨胀 Range（与 GetUnitEdgeDistance 边缘判定一致）
            GizmoDraw.ExpandedBounds(center, worldSize, RangeForPreview, new Color(1f, 0.2f, 0.2f, 0.5f));
        }

        /// <summary>
        /// 从对象池取出时调用（重置状态）
        /// </summary>
        public virtual void OnPoolSpawn()
        {
            // 0. 重置运行时就绪状态，防止旧对象直接进入战斗
            _initialized = false;

            // 1. 先禁用 Animator，防止 SetActive 时从上次状态恢复
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            if (_animator != null) _animator.enabled = false;

            // 2. 激活 GameObject
            gameObject.SetActive(true);

            // 3. 重置 Animator：Rebind 清内部状态 + Play 强制回到 Idle
            if (_animator != null)
            {
                _animator.Rebind();
                _animator.SetInteger("State", 0);
                _animator.ResetTrigger("Death");
                _animator.ResetTrigger("Summon");
                _animator.ResetTrigger("StunHit");
                _animator.ResetTrigger("Shockwave");
                _animator.ResetTrigger("DeathExplosion");
                _animator.ResetTrigger("Burn");
                _animator.ResetTrigger("Splash");
                _animator.ResetTrigger("KingAura");
                _animator.ResetTrigger("Charge");
                _animator.ResetTrigger("Dash");
                _animator.ResetTrigger("BossSkill1");
                _animator.ResetTrigger("BossSkill2");
                _animator.ResetTrigger("BossSkill3");
                _animator.SetBool("Charge", false);
                _animator.SetBool("Taunt", false);
                _animator.SetBool("ShieldWall", false);
                // Play 强制跳转到 Entry（Idle），绕过 AnimatorOverrideController 的状态残留
                _animator.Play(0, 0, 0f);
                _animator.Update(0f);
                _animator.enabled = true;
            }

            IsTauntSource = false;
            TauntRadius = 0f;
            ShieldBlocks = 0;
            DamageAbsorbRemaining = 0f;
            StunTimer = 0f;
            DamageReduction = 0f;
            ShareRedirected = false;
            ClearDoT();
            SlowRestoreTimer = 0f;
            _needsFirstFrameSearch = true;
            SimulatesCombat = SimulatesCombatDefault;
            _currentAnimState = -1;
            OverrideFindTarget = null;
            OverrideAttackRange = null;
            _bonusDamage = 0f;
            OriginalMoveSpeed = 0f;
            _buffs.Clear();
            _hasBaseStats = false;

            // 重置 Stats 中的移速到预制体默认值
            if (_moveSpeed > 0f)
            {
                var s = Stats;
                s.MoveSpeed = _moveSpeed;
                Stats = s;
            }

            // 重新订阅 UnitPassives 的事件（OnPoolDespawn 中已清除）
            var passives = GetComponentInChildren<UnitPassives>();
            if (passives != null)
            {
                passives.ResubscribeEvents();
                passives.RestartRhythm();   // 攻击节拍回到第 1 格
            }
        }

        /// <summary>
        /// 回池时调用（禁用并重置）
        /// </summary>
        public virtual void OnPoolDespawn()
        {
            // 清除每次 Spawn 周期订阅的事件，防止对象池复用后 lambda 累积
            ClearSpawnEvents();
            // 注销盾墙静态缓存（OnDestroy 不会触发，需手动清理）
            var passives = GetComponentInChildren<UnitPassives>();
            if (passives != null) passives.UnregisterShieldWall();

            Target = null;
            CurrentTarget = null;
            _enemyUnits = null;
            _enemyBuildings = null;
            _isDying = false;
            ClearHeightOverride();
            ClearAttackableHeightOverride();
            IsChanneling = false;
            IsPouncing = false;
            if (Passives != null) Passives.AbortPounce();
            if (Passives != null) Passives.ClearLeaderBuffs();
            ClearDoT();
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
            _cachedHitNormalizedTime = -1f;
            SetAnimSpeed(1f);
            _pathDistance = 0f;
            FollowPath = null;
            ClearDoT();
            SlowRestoreTimer = 0f;
            _bonusDamage = 0f;
            OriginalMoveSpeed = 0f;
            StunTimer = 0f;
            DamageReduction = 0f;
            ShieldBlocks = 0;
            DamageAbsorbRemaining = 0f;
            ShareRedirected = false;
            OverrideFindTarget = null;
            OverrideAttackRange = null;
            _buffs.Clear();
            _hasBaseStats = false;

            // 重置 Stats 中的移速到预制体默认值
            if (_moveSpeed > 0f)
            {
                var s = Stats;
                s.MoveSpeed = _moveSpeed;
                Stats = s;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 清除每次 Spawn 周期订阅的事件。
        /// OnDeathEvent / OnHPChanged / OnDied 由 UnitPassives/Audio 在 Awake 中订阅，
        /// 不在此清除（UnitPassives 会在 OnPoolSpawn 中重新订阅 OnAttackEvent）。
        /// </summary>
        private void ClearSpawnEvents()
        {
            OnAttackEvent = null;
            OnTakeDamageEvent = null;
            OnDamageCalculated = null;
            OnHealed = null;
            OnExecuted = null;
            OnKillEvent = null;
            OnSummonFrame = null;
            OnDestroyed = null;
            Summoner = null;
        }
    }
}
