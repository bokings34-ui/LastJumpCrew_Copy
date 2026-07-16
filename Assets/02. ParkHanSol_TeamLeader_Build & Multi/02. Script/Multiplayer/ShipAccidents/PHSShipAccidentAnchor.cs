using System;
using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    [DisallowMultipleComponent]
    public sealed class PHSShipAccidentAnchor : MonoBehaviour, IShipAccidentRepairTarget
    {
        [Header("Anchor Identity")]
        [SerializeField] private string anchorId;
        [SerializeField] private NetworkShipModuleId moduleId = NetworkShipModuleId.LifeSupport;
        [SerializeField] private PHSShipAccidentId[] supportedAccidents = Array.Empty<PHSShipAccidentId>();

        [Header("Presentation")]
        [SerializeField] private Transform presentationRoot;

        private PHSNetworkShipAccidentCoordinator coordinator;
        private PHSShipAccidentDefinitionSO activeDefinition;
        private NetworkShipAccidentSnapshot snapshot;
        private GameObject presentationInstance;
        private uint requestSequence;

        public string AnchorId => anchorId;
        public NetworkShipModuleId ModuleId => moduleId;
        public uint AccidentInstanceId => snapshot.InstanceId;
        public PHSShipAccidentId AccidentId => snapshot.AccidentId;
        public string RequiredItemId => activeDefinition == null
            ? string.Empty
            : activeDefinition.RequiredItemId;
        public Vector3 RepairPosition => transform.position;
        public bool IsRepairComplete => snapshot.InstanceId == 0U || snapshot.IsRepairComplete;
        public string InteractionPrompt => snapshot.InstanceId == 0U || activeDefinition == null
            ? string.Empty
            : $"{activeDefinition.DisplayName} 대응 ({activeDefinition.RequiredItemId})";

        public bool Supports(PHSShipAccidentDefinitionSO definition)
        {
            if (definition == null || definition.TargetModule != moduleId || supportedAccidents == null)
            {
                return false;
            }

            foreach (var supported in supportedAccidents)
            {
                if (supported == definition.Id)
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return snapshot.InstanceId != 0U
                && activeDefinition != null
                && !snapshot.IsRepairComplete
                && itemHolder != null
                && itemHolder.HasItem
                && itemHolder.CurrentItem != null
                && itemHolder.CurrentItem.ItemId == activeDefinition.RequiredItemId;
        }

        public void Interact(IItemHolder itemHolder)
        {
            RequestRepair(itemHolder);
        }

        public bool RequestRepair(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogWarning($"PHS_SHIP_ACCIDENT_REPAIR_REJECTED reason=interaction_contract anchor={anchorId}", this);
                return false;
            }

            if (coordinator == null)
            {
                Debug.LogError($"PHS_SHIP_ACCIDENT_REPAIR_REJECTED reason=coordinator_missing anchor={anchorId}", this);
                return false;
            }

            var holderComponent = itemHolder as Component;
            var itemRecord = holderComponent == null
                ? null
                : holderComponent.GetComponent<NetworkPlayerItemRecord>();
            if (itemRecord == null)
            {
                Debug.LogError($"PHS_SHIP_ACCIDENT_REPAIR_REJECTED reason=item_record_missing anchor={anchorId}", this);
                return false;
            }

            requestSequence++;
            if (requestSequence == 0U)
            {
                requestSequence = 1U;
            }

            return coordinator.RequestRepair(this, itemRecord, requestSequence);
        }

        internal bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(anchorId))
            {
                reason = "anchor_id_missing";
                return false;
            }

            if (moduleId == NetworkShipModuleId.None
                || !Enum.IsDefined(typeof(NetworkShipModuleId), moduleId))
            {
                reason = $"module_id_invalid:{(byte)moduleId}";
                return false;
            }

            if (supportedAccidents == null || supportedAccidents.Length == 0)
            {
                reason = "supported_accidents_missing";
                return false;
            }

            var unique = new System.Collections.Generic.HashSet<PHSShipAccidentId>();
            foreach (var supported in supportedAccidents)
            {
                if (supported == PHSShipAccidentId.None
                    || !Enum.IsDefined(typeof(PHSShipAccidentId), supported))
                {
                    reason = $"supported_accident_invalid:{(ushort)supported}";
                    return false;
                }

                if (!unique.Add(supported))
                {
                    reason = $"supported_accident_duplicate:{supported}";
                    return false;
                }
            }

            if (presentationRoot == null)
            {
                reason = "presentation_root_missing";
                return false;
            }

            reason = null;
            return true;
        }

        internal void Bind(PHSNetworkShipAccidentCoordinator owner)
        {
            coordinator = owner;
        }

        internal void ApplySnapshot(
            NetworkShipAccidentSnapshot currentSnapshot,
            PHSShipAccidentDefinitionSO definition)
        {
            if (definition == null || !Supports(definition))
            {
                Debug.LogError(
                    $"PHS_SHIP_ACCIDENT_PRESENTATION_FAILED reason=definition_not_supported anchor={anchorId} accident={currentSnapshot.AccidentId}",
                    this);
                return;
            }

            var needsNewPresentation = snapshot.InstanceId != currentSnapshot.InstanceId
                || activeDefinition != definition
                || presentationInstance == null;
            snapshot = currentSnapshot;
            activeDefinition = definition;

            if (!needsNewPresentation)
            {
                return;
            }

            DestroyPresentation();
            presentationInstance = Instantiate(definition.PresentationPrefab, presentationRoot);
            presentationInstance.name = $"PHS_Accident_{currentSnapshot.InstanceId}_{definition.Id}";
        }

        internal void ClearSnapshot()
        {
            snapshot = default;
            activeDefinition = null;
            DestroyPresentation();
        }

        private void DestroyPresentation()
        {
            if (presentationInstance == null)
            {
                return;
            }

            Destroy(presentationInstance);
            presentationInstance = null;
        }
    }
}
