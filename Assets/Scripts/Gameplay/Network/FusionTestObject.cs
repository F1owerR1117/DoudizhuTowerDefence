using Fusion;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// Fusion 验证测试对象。
    /// 验证三件事：Tick运行、Authority分配、状态同步。
    /// </summary>
    public class FusionTestObject : NetworkBehaviour
    {
        [Networked]
        public int TestTick { get; set; }

        [Networked]
        public int TestValue { get; set; }

        public override void Spawned()
        {
            // 验证 ② Authority 分配
            string role = Object.HasStateAuthority ? "HOST" : "CLIENT";
            Debug.Log($"[FusionTest] ===== SPAWNED ===== Role={role}, HasStateAuthority={Object.HasStateAuthority}");
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority)
            {
                // 验证 ① Tick 运行 + ③ 状态写入
                TestTick++;
                TestValue = TestTick;

                if (TestTick % 60 == 0)
                    Debug.Log($"[FusionTest] ===== HOST写入 ===== TestTick={TestTick}, TestValue={TestValue}");
            }
        }

        public override void Render()
        {
            if (!Object.HasStateAuthority)
            {
                // 验证 ③ Client 能否看到 Host 的状态变化
                if (TestTick % 60 == 0)
                    Debug.Log($"[FusionTest] ===== CLIENT读取 ===== TestTick={TestTick}, TestValue={TestValue}");
            }
        }
    }
}
