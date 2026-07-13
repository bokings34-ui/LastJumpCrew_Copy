using LastJumpCrew.Common;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class NetworkPlayerKnockbackReceiver : NetworkBehaviour, IKnockbackable
    {
        [Header("References")]

        [SerializeField]
        private NetworkPlayerController playerController;

        [Header("Knockback")]

        [SerializeField, Min(0f)] //너무 강한 힘이 들어왔을 때 날아가는 최대 제한값
        private float maximumKnockbackForce = 8f;

        public bool CanReceiveKnockback => playerController != null;

        private void Awake()
        {
            if(playerController == null)
            {
                playerController = GetComponent<NetworkPlayerController>(); 
            }
        }
        public void ApplyKnockback(Vector3 direction, float force, GameObject attacker)
        {
            if (!CanReceiveKnockback)
            {
                return;
            }
            if (direction.sqrMagnitude <= 0.001f)//방향이 0이면 정상적인 넉백 방향을 계산할 수 없어서 중단하기
            {
                return;
            }
            var clampedFore = Mathf.Clamp(force, 0f, maximumKnockbackForce); //잘못된 큰값이 들어가도 최대값까지만 허용

            var knockbackVelocity = direction.normalized * clampedFore;

            playerController.ApplyExternalVelocity(knockbackVelocity);


        }
        private void Reset()
        {
            playerController = GetComponent<NetworkPlayerController>();
        }
    }
}
