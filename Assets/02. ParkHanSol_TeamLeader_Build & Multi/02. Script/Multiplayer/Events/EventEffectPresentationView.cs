using LastJumpCrew.Common;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class EventEffectPresentationView : MonoBehaviour, IEventRepairTargetHandle
    {
        private const string FireExtinguisherItemId = "fire_extinguisher";
        private const string WrenchItemId = "wrench";

        private NetworkEventEffectSnapshot snapshot;

        public ulong EventInstanceId => snapshot.EventInstanceId;
        public uint EffectInstanceId => snapshot.EffectInstanceId;
        public EventEffectKind EffectKind => snapshot.Kind;
        public string RequiredItemId => snapshot.Kind switch
        {
            EventEffectKind.Fire => FireExtinguisherItemId,
            EventEffectKind.OxygenLeak => WrenchItemId,
            _ => string.Empty
        };
        public string InteractionPrompt => snapshot.Kind switch
        {
            EventEffectKind.Fire => "소화기 필요",
            EventEffectKind.OxygenLeak => "렌치 필요",
            _ => string.Empty
        };

        public void Activate(NetworkEventEffectSnapshot effectSnapshot)
        {
            snapshot = effectSnapshot;
            transform.position = effectSnapshot.WorldPosition;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            snapshot = default;
            gameObject.SetActive(false);
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return snapshot.IsActive
                && !string.IsNullOrEmpty(RequiredItemId)
                && itemHolder != null
                && itemHolder.HasItem
                && itemHolder.CurrentItem != null
                && itemHolder.CurrentItem.ItemId == RequiredItemId;
        }

        public void Interact(IItemHolder itemHolder)
        {
        }
    }
}
