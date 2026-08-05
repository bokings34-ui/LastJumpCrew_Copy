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

        public void ArmServer(float delay)
        {
            if (despawnRoutine != null)
            {
                return;
            }

            if (NetworkObject != null && NetworkObject.IsSpawned && !IsServer)
            {
                Debug.LogError($"PHS_BROKEN_ITEM_DESPAWN_FAILED reason=server_required item={name}", this);
                return;
            }

            despawnRoutine = StartCoroutine(DespawnAfterDelay(Mathf.Max(0f, delay)));
            Debug.Log($"PHS_BROKEN_ITEM_DESPAWN_ARMED item={name} delay={delay:F2}", this);
        }

        private IEnumerator DespawnAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            despawnRoutine = null;
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                if (!IsServer)
                {
                    Debug.LogError($"PHS_BROKEN_ITEM_DESPAWN_FAILED reason=server_required item={name}", this);
                    yield break;
                }

                NetworkObject.Despawn(true);
                yield break;
            }

            Destroy(gameObject);
        }

        public override void OnNetworkDespawn()
        {
            if (despawnRoutine != null)
            {
                StopCoroutine(despawnRoutine);
                despawnRoutine = null;
            }

            base.OnNetworkDespawn();
        }
    }
}
