using UnityEngine;

namespace LastJumpCrew.Common
{
    public interface IHoldableItem
    {
        string ItemId { get; }
        string DisplayName { get; }
        Transform HoldTransform { get; }
        void OnPickedUp(IItemHolder holder);
        void OnDropped(Vector3 dropPosition);
    }
}
