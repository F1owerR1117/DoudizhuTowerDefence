using DoudizhuTower.Gameplay.Battle;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    public partial class UnitPassives
    {
        // ══════════════════════════════════════════
        //  召唤师
        // ══════════════════════════════════════════

        private void UpdateSummoner()
        {
            if (!_owner.IsAlive)
            {
                // 引导中死亡 → 解除站定，避免状态残留
                if (_isSummoning) CancelSummon();
                else _owner.IsChanneling = false;
                return;
            }
            if (summonPrefab == null) return;

            // 眩晕期间不召唤
            if (_owner.StunTimer > 0f)
            {
                if (_isSummoning) CancelSummon();
                return;
            }

            // 正在播放召唤动画 → 等待 Animation Event 触发（带超时兜底，防止永远站定）
            if (_isSummoning)
            {
                _summonChannelTimer += Time.deltaTime;
                if (_summonChannelTimer >= _summonChannelLimit)
                {
                    // 召唤动画事件未触发（未配置动画等）→ 强制完成，避免召唤师被永久锁住
                    OnSummonFrameHandler();
                }
                return;
            }

            // 清理已死亡的召唤物
            _summons.RemoveAll(u => u == null || !u.IsAlive);

            // 定时召唤
            _summonTimer += Time.deltaTime;
            if (_summonTimer >= summonInterval && _summons.Count < maxSummons)
            {
                _summonTimer = 0f;
                StartSummon(_owner.VisualCenter);
            }
        }

        private void OnSummonerKill(CardUnit victim)
        {
            // 启用攻击节拍时，击杀召唤改由「造成击杀的那一段」的 SummonOnKill 决定，
            // 全局的「击杀即召唤」不再无条件触发，避免抢占节拍的编排。
            if (RhythmActive) return;
            TriggerKillSummon(victim, 1);
        }

        /// <summary>
        /// 击杀召唤的统一入口（供全局击杀被动与攻击节拍共用）。
        /// 返回是否真的召唤出了单位。
        /// </summary>
        public bool TriggerKillSummon(CardUnit victim, int count)
        {
            if (victim == null || summonPrefab == null) return false;
            _summons.RemoveAll(u => u == null || !u.IsAlive);
            if (_summons.Count >= maxSummons) return false;
            if (_owner.StunTimer > 0f) return false;
            // 击杀召唤物不会触发重复召唤，防止无限循环
            if (victim.Summoner != null) return false;

            int n = Mathf.Max(1, count);
            int pre = _summons.Count;
            SpawnSummon(victim.VisualCenter, n);
            return _summons.Count > pre;
        }

        /// <summary>开始召唤：播放召唤动画，调整速度对齐间隔</summary>
        private void StartSummon(Vector3 position)
        {
            if (_isSummoning) return;

            // 攻击中不打断，直接生成召唤物
            if (_owner.IsAttacking)
            {
                SpawnSummon(position, Mathf.Max(1, summonCountPerCast));
                return;
            }

            _isSummoning = true;
            // 记录施法瞬间的位置：召唤期间召唤师站定，避免生成点相对召唤师偏移
            _summonPosition = position;
            _owner.IsChanneling = true;
            _summonChannelTimer = 0f;
            _summonChannelLimit = Mathf.Max(0.5f, summonInterval * 0.9f + 0.3f);

            // 订阅召唤帧事件（Animation Event 触发）
            _owner.OnSummonFrame += OnSummonFrameHandler;

            // 调整召唤动画速度，使动画长度对齐召唤间隔
            float clipLen = 0f;
            if (_owner.TryGetComponent<SimpleAnimator>(out var sa) && sa.summonClip != null)
                clipLen = sa.summonClip.length;
            float speed = clipLen > 0f ? Mathf.Min(clipLen / summonInterval, 4f) : 1f;
            _owner.SetAnimSpeedPublic(speed);

            // 播放召唤动画
            _owner.TriggerAnim("Summon");
        }

        /// <summary>Animation Event 回调：召唤帧触发，生成召唤物</summary>
        private void OnSummonFrameHandler()
        {
            _owner.OnSummonFrame -= OnSummonFrameHandler;
            _isSummoning = false;
            _owner.IsChanneling = false;
            _owner.SetAnimSpeedPublic(1f);
            if (_owner.IsAlive)
                SpawnSummon(_summonPosition, Mathf.Max(1, summonCountPerCast));
        }

        /// <summary>取消召唤（被眩晕打断时）</summary>
        private void CancelSummon()
        {
            _owner.OnSummonFrame -= OnSummonFrameHandler;
            _isSummoning = false;
            _owner.IsChanneling = false;
            _owner.SetAnimSpeedPublic(1f);
        }

        /// <summary>
        /// 计算本次实际可生成的数量：
        /// 先清理死亡召唤物，再按 maxSummons 剩余额度截断（剩余 0 则不召唤）。
        /// </summary>
        private int ComputeSummonCount(int requested)
        {
            _summons.RemoveAll(u => u == null || !u.IsAlive);
            int remaining = maxSummons - _summons.Count;
            if (remaining <= 0) return 0;
            return Mathf.Clamp(requested, 1, remaining);
        }

        /// <summary>
        /// 生成召唤物。count = 本次生成数量（受 maxSummons 剩余额度限制）。
        /// 多只时沿水平方向左右排开，避免完全重叠。
        /// </summary>
        private void SpawnSummon(Vector3 position, int count = 1)
        {
            if (summonPrefab == null || _owner == null) return;

            var prefabUnit = summonPrefab.GetComponent<CardUnit>();
            if (prefabUnit == null)
            {
                Debug.LogWarning("[Summoner] summonPrefab 上没有 CardUnit 组件");
                return;
            }

            int spawnCount = ComputeSummonCount(count);
            if (spawnCount <= 0) return;

            // 召唤音效 + 特效（每次召唤播一次）
            _unitAudio?.PlaySummon();
            _unitVFX?.PlaySummon(position);

            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 pos = position + new Vector3(GetSummonOffset(i), 0f, 0f);
                var unit = BattleManager.Instance?.SpawnSummonedUnit(prefabUnit, pos, _owner);
                if (unit != null) _summons.Add(unit);
                if (_summons.Count >= maxSummons) break;
            }
        }

        /// <summary>多只召唤物时的水平偏移：0, +1, -1, +2, -2 …（单位：格）</summary>
        private static float GetSummonOffset(int index)
        {
            if (index <= 0) return 0f;
            int step = (index + 1) / 2;
            float sign = (index % 2 == 1) ? 1f : -1f;
            return sign * step * 0.8f;
        }
    }
}
