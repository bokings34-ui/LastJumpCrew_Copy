using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class NetworkOwnerUiRoot : NetworkBehaviour
    {
        [SerializeField] private GameObject presentationRoot;

        private bool registeredLocalPresentation;

        public static bool HasActiveLocalPresentation { get; private set; }

        private void Awake()
        {
            if (presentationRoot == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_OWNER_UI_SETUP_FAILED reason=presentation_root_missing root={name}",
                    this);
                enabled = false;
                return;
            }

            presentationRoot.SetActive(false);
        }

        public override void OnNetworkSpawn()
        {
            if (presentationRoot == null)
            {
                return;
            }

            presentationRoot.SetActive(IsOwner);
            if (!IsOwner)
            {
                return;
            }

            registeredLocalPresentation = true;
            HasActiveLocalPresentation = true;
        }

        public override void OnNetworkDespawn()
        {
            presentationRoot?.SetActive(false);
            if (!registeredLocalPresentation)
            {
                return;
            }

            registeredLocalPresentation = false;
            HasActiveLocalPresentation = false;
        }

        public override void OnDestroy()
        {
            if (registeredLocalPresentation)
            {
                registeredLocalPresentation = false;
                HasActiveLocalPresentation = false;
            }

            base.OnDestroy();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            HasActiveLocalPresentation = false;
        }
    }
}
