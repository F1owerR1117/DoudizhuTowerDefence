using Fusion;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// 连接成功后自动 Spawn FusionTestObject。
    /// 使用 InvokeRepeating 检测 Runner 状态。
    /// </summary>
    public class FusionTestSpawner : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private NetworkObject testObjectPrefab;

        private bool _spawned = false;

        private void Update()
        {
            if (_spawned || testObjectPrefab == null) return;

            var runner = FindAnyObjectByType<NetworkRunner>();
            if (runner == null) return;

            // 只在 Server/Host 端 Spawn
            if (!runner.IsServer) return;

            runner.Spawn(testObjectPrefab, Vector3.zero, Quaternion.identity);
            _spawned = true;
            Debug.Log("[TestSpawner] FusionTestObject 已 Spawn");
        }
    }
}
