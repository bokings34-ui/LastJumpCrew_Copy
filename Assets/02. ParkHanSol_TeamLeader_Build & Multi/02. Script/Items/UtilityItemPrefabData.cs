using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [CreateAssetMenu(
        fileName = "PHS_UtilityItemPrefabData",
        menuName = "LastJumpCrew/ParkHanSol/Utility Item Prefab Data")]
    public sealed class UtilityItemPrefabData : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject heldPrefab;
        [SerializeField] private GameObject droppedPrefab;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public GameObject HeldPrefab => heldPrefab;
        public GameObject DroppedPrefab => droppedPrefab;
        public bool HasHeldPrefab => heldPrefab != null;
        public bool HasDroppedPrefab => droppedPrefab != null;
    }
}
