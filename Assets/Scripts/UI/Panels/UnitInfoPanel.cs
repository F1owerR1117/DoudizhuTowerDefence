using DoudizhuTower.Gameplay.Entities;
using TMPro;
using UnityEngine;

namespace DoudizhuTower.UI.Panels
{
    /// <summary>
    /// 信息面板（World Space Canvas）。
    /// 绑定 CardUnit（兵种或建筑 _isBuilding），选中时动态实例化，跟随目标移动，面向摄像机。
    /// </summary>
    public class UnitInfoPanel : MonoBehaviour
    {
        [Header("文本引用")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI atkText;
        [SerializeField] private TextMeshProUGUI dpsText;
        [SerializeField] private TextMeshProUGUI speedText;
        [SerializeField] private TextMeshProUGUI rangeText;
        [SerializeField] private TextMeshProUGUI factionText;
        [SerializeField] private TextMeshProUGUI passiveText;

        [Header("跟随设置")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 5f, 0f);

        private CardUnit _unit;
        private Collider2D _targetCollider;
        private Camera _cam;

        public void Bind(CardUnit unit)
        {
            if (unit == null) { Destroy(gameObject); return; }

            _targetCollider = unit.Collider2D;
            _unit = unit;
            _cam = Camera.main;

            _unit.OnHPChanged += OnUnitHPChanged;
            _unit.OnStatsChanged += OnUnitStatsChanged;
            _unit.OnDied += OnUnitDied;

            Refresh();
            UpdatePosition();
        }

        public void Unbind()
        {
            if (_unit != null)
            {
                _unit.OnHPChanged -= OnUnitHPChanged;
                _unit.OnStatsChanged -= OnUnitStatsChanged;
                _unit.OnDied -= OnUnitDied;
                _unit = null;
            }
            _targetCollider = null;
        }

        private void LateUpdate()
        {
            if (_unit == null || !_unit.IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            UpdatePosition();

            if (_cam != null)
                transform.rotation = _cam.transform.rotation;
        }

        private void UpdatePosition()
        {
            Vector2 center = _targetCollider != null
                ? _targetCollider.bounds.center
                : (Vector2)_unit.transform.position;
            transform.position = (Vector3)center + offset;
        }

        private void Refresh()
        {
            if (_unit == null) return;
            var stats = _unit.Stats;

            Set(nameText, _unit.gameObject.name);
            Set(hpText, $"HP: {_unit.CurrentHP:F0}/{stats.HP:F0}");

            if (_unit._isBuilding)
            {
                Set(atkText, "");
                Set(dpsText, "");
                Set(speedText, "");
                Set(rangeText, "");
                Set(passiveText, "");
            }
            else
            {
                Set(atkText, $"ATK: {stats.ATK:F0}");
                Set(dpsText, $"DPS: {stats.DPS:F1}");
                Set(speedText, $"速度: {stats.MoveSpeed:F1}");
                Set(rangeText, $"范围: {stats.Range:F1}");
                RefreshPassives();
            }

            Set(factionText, _unit.IsLandlord ? "地主" : "农民");
        }

        private void OnUnitHPChanged(int unitId, float newHP)
        {
            if (_unit == null) return;
            Refresh();
        }

        private void OnUnitStatsChanged()
        {
            if (_unit == null) return;
            Refresh();
        }

        private void OnUnitDied(int unitId)
        {
            Destroy(gameObject);
        }

        private void RefreshPassives()
        {
            var passives = _unit != null ? _unit.GetComponent<UnitPassives>() : null;
            if (passives == null) { Set(passiveText, ""); return; }

            var sb = new System.Text.StringBuilder();
            if (passives.enableSniper)         Append(sb, "点杀");
            if (passives.enableSwarm)          Append(sb, "人海");
            if (passives.enableCharge)         Append(sb, "冲锋");
            if (passives.enableKingAura)       Append(sb, "君王光环");
            if (passives.enableShieldWall)     Append(sb, "盾墙");
            if (passives.enableTaunt)          Append(sb, "嘲讽");
            if (passives.enableDeathExplosion) Append(sb, "死爆");
            if (passives.enableShieldAbsorb)   Append(sb, "护盾");
            if (passives.enableSlowAura)       Append(sb, "减速");
            if (passives.enableStunOnHit)      Append(sb, "眩晕");
            if (passives.enableTear)           Append(sb, "撕裂");
            if (passives.enableShockwave)      Append(sb, "震波");
            if (passives.enableBurnOnDeath)    Append(sb, "火海");
            if (passives.enableSplash)         Append(sb, passives.splashMode == SplashMode.FacingSweep ? "正面群体攻击" : "群体攻击");
            if (passives.enablePounce)         Append(sb, "飞扑");
            if (passives.enableBleed)          Append(sb, "流血");
            if (passives.enableIgnite)         Append(sb, "点燃");
            if (passives.enableImpair)         Append(sb, "损伤");
            if (passives.enableHeavyStrike)    Append(sb, "重击");
            if (passives.enableOverheat)       Append(sb, "过热");
            if (passives.enableLeader)         Append(sb, "领袖");
            if (passives.enableHeal)           Append(sb, "治疗");
            if (passives.enableExecute)        Append(sb, "处决");

            Set(passiveText, sb.Length > 0 ? sb.ToString() : "无被动");
        }

        private static void Append(System.Text.StringBuilder sb, string name)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(name);
        }

        private static void Set(TextMeshProUGUI tmp, string value)
        {
            if (tmp != null) tmp.text = value;
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
