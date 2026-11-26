using System.Collections.Generic;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 领袖被动：为范围内的友军提供攻击力 / 攻击速度 / 移动速度加成。
    /// 采用「每个领袖一套独立 Buff」的实现：多名领袖的光环可叠加，
    /// 友军离开范围或领袖死亡时立即移除。
    /// </summary>
    public partial class UnitPassives
    {
        [Header("领袖")]
        [Tooltip("启用后为范围内的友军提供攻击力、攻击速度、移动速度加成")]
        public bool enableLeader;
        [Tooltip("光环半径")]
        public float leaderRadius = 4f;
        [Tooltip("攻击力提升百分比 (0.2 = +20%)")]
        public float leaderAtkPct = 0.2f;
        [Tooltip("攻击速度提升百分比 (0.2 = 出手快 20%)")]
        public float leaderAttackSpeedPct = 0.2f;
        [Tooltip("移动速度提升百分比 (0.15 = +15%)")]
        public float leaderMoveSpeedPct = 0.15f;
        [Tooltip("是否也加成自己（默认只加成其他友军）")]
        public bool leaderAffectSelf;

        private readonly List<CardUnit> _ledAllies = new List<CardUnit>();
        private float _leaderScanTimer;
        private const float LeaderScanInterval = 0.25f;
        private string _leaderBuffAtk;
        private string _leaderBuffAs;
        private string _leaderBuffMs;

        /// <summary>当前受本领袖加成的友军数量</summary>
        public int LeaderAllyCount => _ledAllies.Count;

        private void InitLeaderBuffIds()
        {
            int id = _owner != null ? _owner.GetInstanceID() : GetInstanceID();
            _leaderBuffAtk = "leader_atk_" + id;
            _leaderBuffAs = "leader_as_" + id;
            _leaderBuffMs = "leader_ms_" + id;
        }

        private void UpdateLeaderAura()
        {
            if (_owner == null) return;
            if (_leaderBuffAtk == null) InitLeaderBuffIds();

            if (!_owner.IsAlive) { ClearLeaderBuffs(); return; }

            _leaderScanTimer += Time.deltaTime;
            if (_leaderScanTimer < LeaderScanInterval) return;
            _leaderScanTimer = 0f;

            // 收集当前范围内的友军
            var inRange = new List<CardUnit>(8);
            int num = Physics2D.OverlapCircle(_owner.VisualCenter, leaderRadius, _overlapFilter, _overlapBuffer);
            for (int i = 0; i < num; i++)
            {
                var ally = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (ally == null || !ally.IsAlive || ally._isBuilding) continue;
                if (ally.IsLandlord != _owner.IsLandlord) continue;
                if (ally == _owner && !leaderAffectSelf) continue;
                if (!inRange.Contains(ally)) inRange.Add(ally);
            }

            // 离开范围 / 已死亡的 → 移除加成
            for (int i = _ledAllies.Count - 1; i >= 0; i--)
            {
                var a = _ledAllies[i];
                if (a == null || !a.IsAlive || !inRange.Contains(a))
                {
                    RemoveLeaderBuffs(a);
                    _ledAllies.RemoveAt(i);
                }
            }

            // 新进入范围的 → 施加上成
            for (int i = 0; i < inRange.Count; i++)
            {
                var a = inRange[i];
                if (_ledAllies.Contains(a)) continue;
                ApplyLeaderBuffs(a);
                _ledAllies.Add(a);
            }
        }

        private void ApplyLeaderBuffs(CardUnit ally)
        {
            if (ally == null) return;
            if (_leaderBuffAtk == null) InitLeaderBuffIds();

            ally.ApplyBuff(_leaderBuffAtk, new CardUnit.StatBuff(atk: 1f + Mathf.Max(0f, leaderAtkPct)));

            // 攻速百分比 → 攻击间隔乘数（+20% 攻速 = 间隔 × 1/1.2）
            float intervalMult = 1f / (1f + Mathf.Max(0f, leaderAttackSpeedPct));
            ally.ApplyBuff(_leaderBuffAs, new CardUnit.StatBuff(atkInterval: intervalMult));

            ally.ApplyBuff(_leaderBuffMs, new CardUnit.StatBuff(moveSpeed: 1f + Mathf.Max(0f, leaderMoveSpeedPct)));
        }

        private void RemoveLeaderBuffs(CardUnit ally)
        {
            if (ally == null || _leaderBuffAtk == null) return;
            ally.RemoveBuff(_leaderBuffAtk);
            ally.RemoveBuff(_leaderBuffAs);
            ally.RemoveBuff(_leaderBuffMs);
        }

        /// <summary>清除所有友军身上的领袖加成（死亡 / 回池 / 重新订阅时调用）</summary>
        public void ClearLeaderBuffs()
        {
            for (int i = 0; i < _ledAllies.Count; i++)
                RemoveLeaderBuffs(_ledAllies[i]);
            _ledAllies.Clear();
        }
    }
}
