using DoudizhuTower.Core.Battle;
using DoudizhuTower.Gameplay.Battle;
using DoudizhuTower.Gameplay.Entities;
using UnityEngine;

namespace DoudizhuTower.UI.Floating
{
    /// <summary>
    /// 伤害数字对象池。单例，挂载到 World Space Canvas 下。
    /// 自动查找 BattleManager 并订阅 OnUnitSpawned 事件，
    /// 为每个新兵种挂钩伤害飘字。
    /// </summary>
    public class FloatingTextPool : MonoBehaviour
    {
        [Header("预制体与池大小")]
        [SerializeField] private DamageFloatText _prefab;
        [SerializeField] private int _poolSize = 20;

        [Header("显示偏移")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 1.2f, 0f);

        private DamageFloatText[] _pool;
        private BattleManager _battleManager;

        public static FloatingTextPool Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _pool = new DamageFloatText[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                _pool[i] = Instantiate(_prefab, transform);
                _pool[i].gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            // 查找 BattleManager 并订阅单位生成事件
            _battleManager = FindAnyObjectByType<BattleManager>();
            if (_battleManager != null)
            {
                _battleManager.OnUnitSpawned += HandleUnitSpawned;
            }
            else
            {
                Debug.LogWarning("[FloatingTextPool] 未找到 BattleManager，伤害数字不会自动显示");
            }
        }

        private void OnDestroy()
        {
            if (_battleManager != null)
                _battleManager.OnUnitSpawned -= HandleUnitSpawned;
            if (Instance == this)
                Instance = null;
        }

        private void HandleUnitSpawned(CardUnit unit)
        {
            if (unit == null) return;
            unit.OnDamageCalculated += (damage, type) =>
            {
                Spawn(damage, unit.transform.position + _offset, type);
            };
            // 治疗：绿色 +N
            unit.OnHealed += (amount) =>
            {
                Spawn(FloatTextKind.Heal, amount, unit.transform.position + _offset, DamageType.Physical, null);
            };
            // 处决：黄色「处决」
            unit.OnExecuted += () =>
            {
                Spawn(FloatTextKind.Execute, 0f, unit.transform.position + _offset, DamageType.Physical, null);
            };
        }

        /// <summary>
        /// 从池中取出一个飘字并显示
        /// </summary>
        public void Spawn(float damage, Vector3 position, DamageType type)
        {
            Spawn(FloatTextKind.Damage, damage, position, type, null);
        }

        /// <summary>
        /// 从池中取出一个飘字并显示（指定种类）
        /// </summary>
        public void Spawn(FloatTextKind kind, float value, Vector3 position, DamageType type, System.Action onComplete)
        {
            foreach (var text in _pool)
            {
                if (text == null || text.gameObject.activeSelf) continue;
                text.Show(kind, value, position, type, onComplete);
                return;
            }
        }

        /// <summary>
        /// 手动为指定单位挂钩（用于 UnitFactory.Spawn 手动创建的单元）
        /// </summary>
        public void HookUnit(CardUnit unit)
        {
            if (unit == null) return;
            HandleUnitSpawned(unit);
        }
    }
}
