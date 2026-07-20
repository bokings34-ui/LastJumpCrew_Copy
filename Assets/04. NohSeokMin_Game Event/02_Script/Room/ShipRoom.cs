using UnityEngine;

namespace SM
{
    public class ShipRoom : MonoBehaviour, IRoom
    {
        [SerializeField] private string roomId;

        public string RoomId { get { return roomId; } }

        private void OnEnable()
        {
            RoomRegistry.Instance.Register(this);
        }

        private void OnDisable()
        {
            RoomRegistry.Peek()?.Unregister(this);
        }
    }
}