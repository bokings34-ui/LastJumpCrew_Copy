using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class BrokenItemAutoDespawn : NetworkBehaviour
    {
        private Coroutine despawnRoutine;
        private bool isArmed;

        public void ArmServer(float delay)
        {
            if (!IsServer)
            {
                Debug.LogError($"PHS_BROKEN_ITEM_DESPAWN_FAILED " + $"reason=server_required item={name}", this);

                return;
            }
            if (isArmed)
            {
                return;
            }
            isArmed = true;

            var safeDelay = Mathf.Max(0f, delay);
            despawnRoutine = StartCoroutine(DespawnAfterDelay(safeDelay));

            Debug.Log($"PHS_BROKEN_ITEM_DESPAWN_ARMED " + $"item={name} delay={safeDelay:F2}", this);
        }
        private IEnumerator DespawnAfterDelay(float delay)
        {
            if(delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            despawnRoutine = null;

            if (!IsServer)
            {
                yield break;
            }
            if(NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
            else
            {
                StopCoroutine(despawnRoutine);
                despawnRoutine = null;
            }
        }
        public override void OnNetworkDespawn()
        {
            if(despawnRoutine != null)
            {
                StopCoroutine(despawnRoutine);
                despawnRoutine = null;  
            }
            base.OnNetworkDespawn();
        }
    }
}