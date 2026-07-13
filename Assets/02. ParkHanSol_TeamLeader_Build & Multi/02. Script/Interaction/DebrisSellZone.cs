using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class DebrisSellZone : MonoBehaviour
    {
        [SerializeField] private BoxCollider sellTrigger;
        [SerializeField] private PartyDebrisWallet partyWallet;
        [SerializeField] private string debrisTag = "Debris";

        private readonly HashSet<DebrisItem> pendingItems = new();

        private void Awake()
        {
            ValidateSetup();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryGetDebrisItem(other, out var debrisItem))
            {
                return;
            }

            pendingItems.Add(debrisItem);
        }

        private void FixedUpdate()
        {
            if (pendingItems.Count == 0)
            {
                return;
            }

            foreach (var debrisItem in pendingItems)
            {
                if (debrisItem == null)
                {
                    continue;
                }

                var consumedHeldDebris = TryConsumeHeldDebris(debrisItem, out var isHeldDebris);
                if (isHeldDebris && !consumedHeldDebris)
                {
                    // 손에 든 아이템은 Holder 상태까지 함께 비워져야 한다.
                    // 소비에 실패한 상태에서 모델만 제거하면 다음 획득 때 이전 아이템이 다시 생성된다.
                    continue;
                }

                partyWallet.AddCredits(debrisItem.Value);
                Debug.Log($"PHS_DEBRIS_SOLD zone={name} debris={debrisItem.name} value={debrisItem.Value}");

                // 월드 데브리만 여기서 직접 제거한다. 손에 든 데브리는 Holder가
                // 모델, 보유 데이터, HUD를 한 번에 정리한다.
                if (!isHeldDebris)
                {
                    Destroy(debrisItem.gameObject);
                }
            }

            pendingItems.Clear();
        }

        private bool TryGetDebrisItem(Collider other, out DebrisItem debrisItem)
        {
            debrisItem = null;

            if (other == null)
            {
                return false;
            }

            var colliderHasDebrisTag = other.CompareTag(debrisTag);
            debrisItem = other.GetComponentInParent<DebrisItem>();
            if (debrisItem == null)
            {
                if (colliderHasDebrisTag)
                {
                    Debug.LogError($"PHS_DEBRIS_SELL_FAILED reason=debris_item_missing zone={name} target={other.name}");
                }

                return false;
            }

            if (!debrisItem.CompareTag(debrisTag))
            {
                return false;
            }

            return ValidateSetup();
        }

        private bool TryConsumeHeldDebris(DebrisItem debrisItem, out bool isHeldDebris)
        {
            isHeldDebris = false;

            var itemObject = debrisItem.GetComponentInParent<UtilityItemObject>();
            if (itemObject == null || !itemObject.IsHeld)
            {
                return false;
            }

            isHeldDebris = true;

            var itemPrefabData = itemObject.ItemPrefabData;
            if (itemPrefabData == null || string.IsNullOrWhiteSpace(itemPrefabData.ItemId))
            {
                Debug.LogError($"PHS_DEBRIS_SELL_FAILED reason=held_item_data_missing zone={name} debris={debrisItem.name}");
                return false;
            }

            var itemHolder = debrisItem.GetComponentInParent<TempPlayerItemHolder>();
            if (itemHolder == null)
            {
                Debug.LogError($"PHS_DEBRIS_SELL_FAILED reason=held_item_holder_missing zone={name} debris={debrisItem.name}");
                return false;
            }

            if (!itemHolder.TryConsumeHeldItem(itemPrefabData.ItemId))
            {
                Debug.LogError($"PHS_DEBRIS_SELL_FAILED reason=held_item_consume_failed zone={name} debris={debrisItem.name} item={itemPrefabData.ItemId}");
                return false;
            }

            return true;
        }

        private bool ValidateSetup()
        {
            if (sellTrigger == null)
            {
                Debug.LogError($"PHS_DEBRIS_SELL_SETUP_FAILED reason=sell_trigger_missing zone={name}");
                return false;
            }

            if (!sellTrigger.isTrigger)
            {
                Debug.LogError($"PHS_DEBRIS_SELL_SETUP_FAILED reason=sell_trigger_not_trigger zone={name}");
                return false;
            }

            if (partyWallet == null)
            {
                Debug.LogError($"PHS_DEBRIS_SELL_SETUP_FAILED reason=party_wallet_missing zone={name}");
                return false;
            }

            return true;
        }
    }
}
