using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSNetworkFoamGunController : NetworkBehaviour
    {
        [Header("Inspector References")]
        [SerializeField] private Camera ownerAimCamera;
        [SerializeField] private Transform serverOrigin;
        [SerializeField] private NetworkPlayerItemRecord itemRecord;
        [SerializeField] private NetworkPlayerLifeState lifeState;
        [SerializeField] private PHSNetworkItemUseActionController actionController;
        [SerializeField] private PHSNetworkItemUseFeedbackController feedbackController;

        [Header("Fire Contract")]
        [SerializeField, Min(0.05f)] private float fireIntervalSeconds = 0.125f;
        [SerializeField, Min(0.1f)] private float maximumOriginError = 1.25f;
        [SerializeField, Range(1f, 90f)] private float maximumYawError = 35f;
        [SerializeField, Range(1f, 89f)] private float maximumPitch = 80f;
        [SerializeField, Min(0.05f)] private float telegraphIntervalSeconds = 0.5f;
        [SerializeField, Min(0.01f)] private float telegraphRadius = 0.12f;
        [SerializeField, Min(0.1f)] private float telegraphDistance = 8f;

        private uint localShotSequence;
        private uint lastServerShotSequence;
        private double nextServerShotTime;
        private float nextLocalShotTime;
        private float nextTelegraphTime;
        private bool setupValid;

        public bool HasRequiredReferences => ownerAimCamera != null
            && serverOrigin != null
            && itemRecord != null
            && lifeState != null
            && actionController != null
            && feedbackController != null;
        public Camera OwnerAimCamera => ownerAimCamera;
        public Transform ServerOrigin => serverOrigin;
        public NetworkPlayerItemRecord ItemRecord => itemRecord;
        public NetworkPlayerLifeState LifeState => lifeState;
        public PHSNetworkItemUseActionController ActionController =>
            actionController;
        public PHSNetworkItemUseFeedbackController FeedbackController =>
            feedbackController;
        public float FireIntervalSeconds => fireIntervalSeconds;
        public float MaximumOriginError => maximumOriginError;
        public float MaximumYawError => maximumYawError;
        public float MaximumPitch => maximumPitch;
        public float TelegraphIntervalSeconds => telegraphIntervalSeconds;
        public float TelegraphRadius => telegraphRadius;
        public float TelegraphDistance => telegraphDistance;
        public bool CanRequestFire => setupValid
            && IsSpawned
            && IsOwner
            && lifeState.IsAlive
            && itemRecord.HeldItemId == PHSNetworkFoamCoordinator.FoamItemId
            && itemRecord.CurrentDurability > 0;

        private void Awake()
        {
            setupValid = ValidateSetup(out var reason);
            if (!setupValid)
            {
                Debug.LogError(
                    $"PHS_FOAM_GUN_SETUP_FAILED reason={reason} player={name}",
                    this);
                enabled = false;
            }
        }

        public bool TryRequestFire()
        {
            if (!CanRequestFire || Time.unscaledTime < nextLocalShotTime)
            {
                return false;
            }

            var aimTransform = ownerAimCamera.transform;
            var origin = aimTransform.position;
            var direction = aimTransform.forward.normalized;
            nextLocalShotTime = Time.unscaledTime + fireIntervalSeconds;
            actionController.TryBeginVisualAction(
                PHSItemUseActionKind.FireExtinguisher,
                Mathf.Min(0.12f, fireIntervalSeconds));

            if (Time.unscaledTime >= nextTelegraphTime)
            {
                nextTelegraphTime = Time.unscaledTime + telegraphIntervalSeconds;
                feedbackController.ShowOwnerLocalTelegraph(
                    PHSItemUseFeedbackKind.FireExtinguisher,
                    PHSItemUseFeedbackShape.Cast,
                    origin,
                    direction,
                    telegraphRadius,
                    telegraphDistance);
            }

            localShotSequence++;
            if (localShotSequence == 0U)
            {
                localShotSequence = 1U;
            }

            RequestFireServerRpc(origin, direction, localShotSequence);
            GetComponent<PHSNetworkItemInteractionAudioRelay>()
                ?.TryPlayOwnerPredicted(NetworkAudioCue.FoamShot);
            return true;
        }

        [ServerRpc]
        private void RequestFireServerRpc(
            Vector3 clientOrigin,
            Vector3 clientDirection,
            uint shotSequence,
            ServerRpcParams rpcParams = default)
        {
            var senderClientId = rpcParams.Receive.SenderClientId;
            if (senderClientId != OwnerClientId
                || shotSequence == 0U
                || shotSequence <= lastServerShotSequence)
            {
                RejectServerShot("sender_or_sequence", senderClientId, shotSequence);
                return;
            }

            if (!NetworkShopTransitionVoteCoordinator.TryAuthorizeHeldItemUseServer(
                    senderClientId,
                    PHSNetworkFoamCoordinator.FoamItemId,
                    out var policyReason))
            {
                RejectServerShot(policyReason, senderClientId, shotSequence);
                return;
            }

            lastServerShotSequence = shotSequence;
            var now = NetworkManager.ServerTime.Time;
            if (now < nextServerShotTime)
            {
                RejectServerShot("rate_limit", senderClientId, shotSequence);
                return;
            }

            nextServerShotTime = now + fireIntervalSeconds;

            if (!setupValid
                || !IsSpawned
                || !IsServer
                || !lifeState.IsAlive
                || itemRecord.HeldItemId
                    != PHSNetworkFoamCoordinator.FoamItemId
                || itemRecord.CurrentDurability <= 0)
            {
                RejectServerShot("player_or_item_contract", senderClientId, shotSequence);
                return;
            }

            if (!IsFinite(clientOrigin)
                || !IsFinite(clientDirection)
                || clientDirection.sqrMagnitude < 0.99f
                || clientDirection.sqrMagnitude > 1.01f
                || Vector3.Distance(clientOrigin, serverOrigin.position)
                    > maximumOriginError
                || !IsDirectionAllowed(clientDirection))
            {
                RejectServerShot("aim_contract", senderClientId, shotSequence);
                return;
            }

            var coordinator = PHSNetworkFoamCoordinator.Instance;
            if (coordinator == null)
            {
                RejectServerShot(
                    "coordinator_missing",
                    senderClientId,
                    shotSequence);
                return;
            }

            if (!coordinator.TrySpawnShotServer(
                    NetworkObject,
                    serverOrigin.position,
                    clientDirection.normalized,
                    shotSequence,
                    out var reason))
            {
                RejectServerShot(
                    reason,
                    senderClientId,
                    shotSequence);
                return;
            }

        }

        private bool IsDirectionAllowed(Vector3 direction)
        {
            var normalized = direction.normalized;
            var planarDirection = Vector3.ProjectOnPlane(normalized, Vector3.up);
            var planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (planarDirection.sqrMagnitude < 0.001f
                || planarForward.sqrMagnitude < 0.001f)
            {
                return false;
            }

            var yaw = Vector3.Angle(planarForward, planarDirection);
            var pitch = Mathf.Abs(90f - Vector3.Angle(normalized, Vector3.up));
            return yaw <= maximumYawError && pitch <= maximumPitch;
        }

        private void RejectServerShot(
            string reason,
            ulong senderClientId,
            uint shotSequence)
        {
            Debug.LogWarning(
                $"PHS_FOAM_SHOT_REJECTED reason={reason} player={name} sender={senderClientId} sequence={shotSequence}",
                this);
        }

        private bool ValidateSetup(out string reason)
        {
            if (!HasRequiredReferences)
            {
                reason = "inspector_reference_missing";
                return false;
            }

            if (fireIntervalSeconds <= 0f
                || maximumOriginError <= 0f
                || maximumYawError <= 0f
                || maximumPitch <= 0f
                || telegraphIntervalSeconds <= 0f
                || telegraphRadius <= 0f
                || telegraphDistance <= 0f)
            {
                reason = "configuration_invalid";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
