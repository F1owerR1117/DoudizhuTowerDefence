using System.Collections;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 飞扑被动：目标在「攻击射程之外、飞扑范围之内」时，
    /// 沿抛物线轨迹跃向目标，落点位于射程边缘（落地即可攻击）。
    /// 飞行期间无视碰撞；落地后重新吸附回路径。
    /// </summary>
    public partial class UnitPassives
    {
        [Header("飞扑")]
        [Tooltip("启用后：目标在攻击射程外、但在此范围内时，抛物线跃到目标脸上")]
        public bool enablePounce;
        [Tooltip("飞扑触发范围（碰撞体边缘距离，格）。目标必须同时在攻击射程之外")]
        public float pounceRange = 8f;
        [Tooltip("飞扑时长（秒）")]
        public float pounceDuration = 0.35f;
        [Tooltip("抛物线弧高（格）")]
        public float pounceArcHeight = 1.5f;
        [Tooltip("飞扑飞行期间是否无敌")]
        public bool pounceInvulnerable;
        [Tooltip("飞扑是否能被眩晕打断（打断后原地落地，不平移）")]
        public bool pounceInterruptible = true;
        [Tooltip("飞扑期间屏蔽其他一次性动画（死亡与飞扑本身除外），避免边飞边播眩晕/震波等")]
        public bool pounceBlockOneShotAnim = true;

        private bool _isPouncing;
        private Coroutine _pounceRoutine;

        /// <summary>是否正在飞扑</summary>
        public bool IsPouncing => _isPouncing;

        /// <summary>
        /// AI 调用：满足条件时开始飞扑，返回 true。
        /// 条件：目标在射程外、且在飞扑范围内；高度可打；非建筑；自身未眩晕。
        /// </summary>
        public bool TryStartPounce(CardUnit target)
        {
            if (!enablePounce || _isPouncing) return false;
            if (_owner == null || !_owner.IsAlive || !_owner.SimulatesCombat) return false;
            if (_owner.StunTimer > 0f) return false;
            if (target == null || !target.IsAlive || target == _owner) return false;
            if (target.IsLandlord == _owner.IsLandlord) return false;   // 只扑敌人
            if (target._isBuilding) return false;                        // 不扑建筑
            if (!_owner.CanAttackHeight(target.UnitHeight)) return false;

            float range = _owner.GetEffectiveRange(target);
            float dist = _owner.GetUnitEdgeDistance(target);
            if (dist <= range + 0.01f) return false;                     // 已经打得到 → 正常攻击
            if (dist > Mathf.Max(pounceRange, range + 0.1f)) return false; // 太远 → 正常走路

            _isPouncing = true;
            _owner.IsPouncing = true;
            _owner.TriggerAnim("Pounce");
            _unitVFX?.PlayPounce(_owner.transform.position);   // 起跳特效
            _unitAudio?.PlayPounce();
            _pounceRoutine = _owner.StartCoroutine(PounceRoutine(target, range));
            return true;
        }

        /// <summary>中断飞扑（回池 / 死亡时调用）</summary>
        public void AbortPounce()
        {
            if (_pounceRoutine != null && _owner != null)
                _owner.StopCoroutine(_pounceRoutine);
            _pounceRoutine = null;
            if (_isPouncing)
            {
                _isPouncing = false;
                if (_owner != null) _owner.IsPouncing = false;
            }
        }

        private IEnumerator PounceRoutine(CardUnit target, float landRange)
        {
            float duration = Mathf.Max(0.05f, pounceDuration);
            float elapsed = 0f;

            Vector3 startPos = _owner.transform.position;
            Vector3 groundPos = startPos;

            // 碰撞箱中心相对 transform 的偏移：**只在开始时采样一次**
            // （飞行中 transform 每帧变化，若每帧重算偏移会与碰撞箱世界坐标互相反馈、导致落点漂移）
            Vector3 myOffset = (Vector3)_owner.VisualCenter - _owner.transform.position;
            Vector3 targetOffset = target != null
                ? (Vector3)target.VisualCenter - target.transform.position
                : Vector3.zero;

            // 朝向：从目标指向自己（只用水平分量，避免弧高污染）
            Vector2 dir = (Vector2)(startPos + myOffset) - ((Vector2)target.transform.position + (Vector2)targetOffset);
            Vector2 flat = new Vector2(dir.x, 0f);
            if (flat.sqrMagnitude < 0.0001f) flat = new Vector2(1f, 0f);
            flat.Normalize();

            bool wasInvulnerable = _owner.Invulnerable;
            bool grantInvuln = pounceInvulnerable && !wasInvulnerable;
            if (grantInvuln) _owner.Invulnerable = true;

            while (elapsed < duration)
            {
                if (_owner == null || !_owner.IsAlive) break;
                if (target == null || !target.IsAlive) break;                       // 目标没了 → 就地落下
                if (pounceInterruptible && _owner.StunTimer > 0f) break;            // 被眩晕打断 → 就地落下

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector3 landPos = CalcLandingPosition(target, landRange, flat, myOffset, targetOffset);
                groundPos = Vector3.Lerp(startPos, landPos, t);

                Vector3 pos = groundPos;
                pos.y += Mathf.Sin(t * Mathf.PI) * pounceArcHeight;                 // 抛物弧
                _owner.transform.position = pos;

                yield return null;
            }

            // 结束（正常完成 / 被打断）：落回地面位置，去掉弧高残留
            if (_owner != null && _owner.IsAlive)
            {
                _owner.transform.position = groundPos;
                _unitVFX?.PlayPounce(groundPos);   // 落地特效
            }

            if (grantInvuln && _owner != null) _owner.Invulnerable = false;

            _isPouncing = false;
            if (_owner != null) _owner.IsPouncing = false;
            _pounceRoutine = null;

            // 落地校正：确保一定落在「能打到」的距离内（最多微调一次）
            if (_owner != null && _owner.IsAlive && target != null && target.IsAlive)
            {
                Physics2D.SyncTransforms();
                float gap = _owner.GetUnitEdgeDistance(target);
                if (gap > landRange)
                {
                    Vector2 toTarget = (Vector2)target.VisualCenter - (Vector2)_owner.VisualCenter;
                    if (toTarget.sqrMagnitude < 0.0001f) toTarget = Vector2.right;
                    _owner.transform.position += (Vector3)(toTarget.normalized * (gap - landRange + 0.05f));
                }
            }

            // 飞扑脱离路径 → 重新吸附
            if (_owner != null && _owner.IsAlive)
                _owner.ResnapToClosestPath();
        }

        /// <summary>
        /// 计算落点（返回 transform 坐标）：目标之外 landRange 处，与目标同高。
        /// 所有碰撞箱偏移都在开始飞扑时采样并传入，避免逐帧反馈。
        /// </summary>
        private Vector3 CalcLandingPosition(CardUnit target, float landRange,
                                            Vector2 flat, Vector3 myOffset, Vector3 targetOffset)
        {
            Vector2 targetCenter = (Vector2)target.transform.position + (Vector2)targetOffset;

            // 中心距 ≈ 射程 + 目标碰撞半径 + 自身碰撞半径的一半，落地后再用边缘距校正
            float back = landRange + target.Stats.CollisionRadius + _owner.Stats.CollisionRadius * 0.5f;
            Vector2 landCenter = new Vector2(targetCenter.x + flat.x * back, targetCenter.y);

            return new Vector3(landCenter.x, landCenter.y, _owner.transform.position.z) - myOffset;
        }
    }
}
