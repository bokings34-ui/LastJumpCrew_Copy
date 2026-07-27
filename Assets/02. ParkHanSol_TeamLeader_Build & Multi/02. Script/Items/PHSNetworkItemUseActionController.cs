using System;
using System.Collections;
using LastJumpCrew.ParkHanSol.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public enum PHSItemUseActionKind : byte
    {
        Generic = 1,
        Wrench = 2,
        FireExtinguisher = 3,
        Battery = 4
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(TempPlayerItemHolder))]
    public sealed class PHSNetworkItemUseActionController : NetworkBehaviour
    {
        [SerializeField] private TempPlayerItemHolder itemHolder;
        [SerializeField] private PHSUtilityFamilyUseVfxPresenter useVfxPresenter;
        [SerializeField, Min(0.05f)] private float defaultDuration = 0.28f;
        [SerializeField, Range(0.05f, 0.95f)] private float defaultImpactNormalizedTime = 0.58f;
        [SerializeField, Range(1f, 120f)] private float swingDegrees = 42f;
        [SerializeField, Range(0f, 45f)] private float rollDegrees = 8f;

        private Coroutine localActionRoutine;
        private Coroutine remoteActionRoutine;
        private uint localActionSequence;

        public event Action<PHSItemUseActionKind> LocalActionStarted;

        private void Awake()
        {
            if (itemHolder == null)
            {
                itemHolder = GetComponent<TempPlayerItemHolder>();
            }

            if (useVfxPresenter == null)
            {
                useVfxPresenter = GetComponent<PHSUtilityFamilyUseVfxPresenter>();
            }

            if (itemHolder == null)
            {
                Debug.LogError(
                    $"PHS_ITEM_ACTION_SETUP_FAILED reason=item_holder_missing player={name}",
                    this);
                enabled = false;
            }
        }

        public bool TryBeginImpactAction(
            PHSItemUseActionKind actionKind,
            Action impactAction,
            float duration = -1f,
            float impactNormalizedTime = -1f)
        {
            if (impactAction == null)
            {
                Debug.LogError(
                    $"PHS_ITEM_ACTION_FAILED reason=impact_missing player={name} action={actionKind}",
                    this);
                return false;
            }

            if (IsSpawned && !IsOwner)
            {
                Debug.LogError(
                    $"PHS_ITEM_ACTION_FAILED reason=owner_required player={name} action={actionKind}",
                    this);
                return false;
            }

            if (localActionRoutine != null)
            {
                return false;
            }

            var resolvedDuration = ResolveDuration(duration);
            var resolvedImpactTime = Mathf.Clamp01(
                impactNormalizedTime < 0f
                    ? defaultImpactNormalizedTime
                    : impactNormalizedTime);
            var sequence = NextLocalActionSequence();
            localActionRoutine = StartCoroutine(
                RunAction(
                    actionKind,
                    resolvedDuration,
                    resolvedImpactTime,
                    impactAction,
                    true));
            LocalActionStarted?.Invoke(actionKind);
            BroadcastAction(actionKind, resolvedDuration, sequence);
            return true;
        }

        public bool TryBeginVisualAction(
            PHSItemUseActionKind actionKind,
            float duration = -1f)
        {
            if (IsSpawned && !IsOwner)
            {
                return false;
            }

            if (localActionRoutine != null)
            {
                return false;
            }

            var resolvedDuration = ResolveDuration(duration);
            var sequence = NextLocalActionSequence();
            localActionRoutine = StartCoroutine(
                RunAction(actionKind, resolvedDuration, 1f, null, true));
            LocalActionStarted?.Invoke(actionKind);
            BroadcastAction(actionKind, resolvedDuration, sequence);
            return true;
        }

        private void BroadcastAction(
            PHSItemUseActionKind actionKind,
            float duration,
            uint sequence)
        {
            if (IsSpawned)
            {
                RequestActionServerRpc((byte)actionKind, duration, sequence);
            }
        }

        [ServerRpc]
        private void RequestActionServerRpc(
            byte actionKindValue,
            float duration,
            uint sequence,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId
                || sequence == 0U
                || !Enum.IsDefined(typeof(PHSItemUseActionKind), actionKindValue)
                || duration < 0.05f
                || duration > 2f)
            {
                Debug.LogWarning(
                    $"PHS_ITEM_ACTION_REJECTED player={name} action={actionKindValue} sequence={sequence}",
                    this);
                return;
            }

            PlayActionClientRpc(actionKindValue, duration, sequence);
        }

        [ClientRpc]
        private void PlayActionClientRpc(
            byte actionKindValue,
            float duration,
            uint sequence)
        {
            if (IsOwner)
            {
                return;
            }

            if (remoteActionRoutine != null)
            {
                StopCoroutine(remoteActionRoutine);
            }

            remoteActionRoutine = StartCoroutine(
                RunAction(
                    (PHSItemUseActionKind)actionKindValue,
                    duration,
                    1f,
                    null,
                    false));
        }

        private IEnumerator RunAction(
            PHSItemUseActionKind actionKind,
            float duration,
            float impactNormalizedTime,
            Action impactAction,
            bool localAction)
        {
            var visual = itemHolder.HeldPresentationTransform;
            if (visual == null)
            {
                Debug.LogError(
                    $"PHS_ITEM_ACTION_FAILED reason=held_visual_missing player={name} action={actionKind}",
                    this);
                ClearRoutine(localAction);
                yield break;
            }

            var initialPosition = visual.localPosition;
            var initialRotation = visual.localRotation;
            useVfxPresenter?.Play(actionKind);
            var elapsed = 0f;
            var impactSent = false;

            while (elapsed < duration && visual != null)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var arc = Mathf.Sin(normalized * Mathf.PI);
                visual.localPosition = initialPosition
                    + new Vector3(0f, -0.025f * arc, 0.035f * arc);
                visual.localRotation = initialRotation
                    * Quaternion.Euler(-swingDegrees * arc, 0f, rollDegrees * arc);

                if (!impactSent && normalized >= impactNormalizedTime)
                {
                    impactSent = true;
                    impactAction?.Invoke();
                }

                yield return null;
            }

            if (visual != null)
            {
                visual.SetLocalPositionAndRotation(initialPosition, initialRotation);
            }

            if (!impactSent)
            {
                impactAction?.Invoke();
            }

            ClearRoutine(localAction);
        }

        private void ClearRoutine(bool localAction)
        {
            if (localAction)
            {
                localActionRoutine = null;
            }
            else
            {
                remoteActionRoutine = null;
            }
        }

        private float ResolveDuration(float duration)
        {
            return Mathf.Clamp(
                duration < 0f ? defaultDuration : duration,
                0.05f,
                2f);
        }

        private uint NextLocalActionSequence()
        {
            localActionSequence++;
            if (localActionSequence == 0U)
            {
                localActionSequence = 1U;
            }

            return localActionSequence;
        }
    }
}
