using System.Collections.Generic;
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

                partyWallet.AddCredits(debrisItem.Value);
                Debug.Log($"PHS_DEBRIS_SOLD zone={name} debris={debrisItem.name} value={debrisItem.Value}");
                Destroy(debrisItem.gameObject);
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
