using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 玩家输入处理器（薄包装层）。
    /// UI → FusionGameManager.SetLocalInput() → FusionService.OnInput() → Fusion 网络同步。
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private FusionGameManager gameManager;

        public void Setup(FusionGameManager manager)
        {
            gameManager = manager;
        }

        /// <summary>出牌操作（支持任意数量牌）</summary>
        public void PlayCard(byte[] cardDeckIndices, int routeIndex = 0, int baseIndex = 0)
        {
            if (gameManager == null) return;
            int slot = gameManager.GetLocalSlot();
            if (slot < 0) return;
            if (gameManager.HasStateAuthority)
                gameManager.SetPlayCardInput(cardDeckIndices, routeIndex, baseIndex);
            else
                gameManager.RpcPlayCard(slot, cardDeckIndices, routeIndex, baseIndex);
        }

        /// <summary>单张牌出牌</summary>
        public void PlayCard(byte cardDeckIndex, int routeIndex = 0, int baseIndex = 0)
        {
            PlayCard(new byte[] { cardDeckIndex }, routeIndex, baseIndex);
        }

        /// <summary>摸牌操作</summary>
        public void DrawCard()
        {
            if (gameManager == null) return;
            int slot = gameManager.GetLocalSlot();
            if (slot < 0) return;
            if (gameManager.HasStateAuthority)
                gameManager.SetDrawInput();
            else
                gameManager.RpcDrawCard(slot);
        }

        /// <summary>叫分操作</summary>
        public void Bid(byte bidValue)
        {
            if (gameManager == null) return;
            int slot = gameManager.GetLocalSlot();
            if (slot < 0) return;
            if (gameManager.HasStateAuthority)
                gameManager.SetBidInput(slot, bidValue);
            else
                gameManager.RpcSubmitBid(slot, bidValue);
        }

        /// <summary>领域激活</summary>
        public void ActivateDomain()
        {
            if (gameManager == null) return;
            gameManager.SetDomainInput();
        }
    }
}
