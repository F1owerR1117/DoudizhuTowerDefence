using System.Collections.Generic;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Systems
{
    /// <summary>
    /// 特效池化服务：统一管理所有游戏特效的对象池生成和回收。
    /// 采用对象池模式，避免频繁创建销毁导致的 GC 压力。
    ///
    /// 特效预制体由各调用方（UnitVFX、Projectile 等）自行配置，
    /// VFXManager 仅提供池化服务。
    ///
    /// 使用方法：
    /// 1. 调用方持有自己的特效预制体引用
    /// 2. 通过 VFXManager.Instance.SpawnVFX(prefab, pos) 生成特效
    /// 3. 特效播放完毕后自动回收到对象池
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        #region 配置

        [Header("-- 池化设置 --")]
        [Tooltip("特效池初始大小")]
        [SerializeField] private int poolInitialSize = 10;
        [Tooltip("特效池最大大小")]
        [SerializeField] private int poolMaxSize = 50;

        #endregion

        #region 对象池

        /// <summary>特效对象池（按预制体分池）</summary>
        private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new();

        /// <summary>正在使用的特效实例</summary>
        private readonly HashSet<GameObject> _activeInstances = new();

        #endregion

        #region 单例

        private static VFXManager _instance;
        public static VFXManager Instance => _instance;

        #endregion

        #region 生命周期

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region 公共方法 - 池化服务

        /// <summary>
        /// 从对象池生成特效实例。
        /// </summary>
        /// <param name="prefab">特效预制体</param>
        /// <param name="position">生成位置</param>
        /// <param name="parent">父物体（可选）</param>
        /// <param name="duration">持续时间（0=使用粒子系统默认时长）</param>
        /// <param name="autoReturn">是否自动回收到池（false=调用方手动管理生命周期）</param>
        /// <returns>特效实例</returns>
        public GameObject SpawnVFX(GameObject prefab, Vector3 position,
            Transform parent = null, float duration = 0f, bool autoReturn = true)
        {
            if (prefab == null) return null;

            // 从对象池获取或创建
            GameObject vfx = GetFromPool(prefab, position, parent);

            if (autoReturn)
            {
                // 设置持续时间
                float destroyTime = duration > 0f ? duration : GetDefaultDuration(prefab);
                if (destroyTime > 0f)
                {
                    var autoComp = vfx.GetComponent<AutoReturnToPool>();
                    if (autoComp == null)
                        autoComp = vfx.AddComponent<AutoReturnToPool>();
                    autoComp.Initialize(this, prefab, destroyTime);
                }
            }

            return vfx;
        }

        /// <summary>
        /// 回收特效到对象池。
        /// </summary>
        public void ReturnToPool(GameObject vfx, GameObject prefab)
        {
            if (vfx == null) return;

            vfx.SetActive(false);
            _activeInstances.Remove(vfx);

            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new Queue<GameObject>();
                _pools[prefab] = pool;
            }

            if (pool.Count < poolMaxSize)
            {
                pool.Enqueue(vfx);
            }
            else
            {
                Destroy(vfx);
            }
        }

        #endregion

        #region 内部方法

        private GameObject GetFromPool(GameObject prefab, Vector3 position, Transform parent)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new Queue<GameObject>();
                _pools[prefab] = pool;

                int warmCount = Mathf.Clamp(poolInitialSize, 0, Mathf.Max(0, poolMaxSize));
                for (int i = 0; i < warmCount; i++)
                {
                    var prewarmed = Instantiate(prefab, transform);
                    prewarmed.name = prefab.name;
                    prewarmed.SetActive(false);
                    pool.Enqueue(prewarmed);
                }
            }

            if (pool.Count > 0)
            {
                var vfx = pool.Dequeue();
                if (vfx != null)
                {
                    vfx.transform.position = position;
                    vfx.transform.SetParent(parent);

                    // 先禁用再启用，利用 m_KeepAnimatorStateOnDisable=0 重置 Animator 状态
                    vfx.SetActive(false);
                    vfx.SetActive(true);

                    // 兜底：显式重置动画到首帧
                    var anim = vfx.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        anim.Rebind();
                        anim.Update(0f);
                    }

                    // 重置粒子系统
                    var ps = vfx.GetComponentInChildren<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Clear(true);
                        ps.Play(true);
                    }

                    _activeInstances.Add(vfx);
                    return vfx;
                }
            }

            // 池中没有，创建新实例
            var newInstance = Instantiate(prefab, position, Quaternion.identity, parent);
            newInstance.name = prefab.name;
            _activeInstances.Add(newInstance);
            return newInstance;
        }

        private float GetDefaultDuration(GameObject prefab)
        {
            // 尝试从特效的粒子系统获取持续时间
            var ps = prefab.GetComponent<ParticleSystem>();
            if (ps != null)
                return ps.main.duration + ps.main.startLifetime.constantMax;

            // 默认 2 秒
            return 2f;
        }

        #endregion
    }

    /// <summary>
    /// 跟随目标组件：让特效持续跟随指定目标。
    /// </summary>
    public class FollowTarget : MonoBehaviour
    {
        private Transform _target;
        private Vector3 _offset;

        public void Initialize(Transform target, Vector3 offset)
        {
            _target = target;
            _offset = offset;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = _target.position + _offset;
        }
    }

    /// <summary>
    /// 自动回收组件：特效播放完毕后自动回收到对象池。
    /// </summary>
    public class AutoReturnToPool : MonoBehaviour
    {
        private VFXManager _manager;
        private GameObject _prefab;
        private float _timer;

        public void Initialize(VFXManager manager, GameObject prefab, float duration)
        {
            _manager = manager;
            _prefab = prefab;
            _timer = duration;
            enabled = true;  // 重新启用组件（上次回收时被禁用）
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _manager?.ReturnToPool(gameObject, _prefab);
                enabled = false;
            }
        }
    }
}
