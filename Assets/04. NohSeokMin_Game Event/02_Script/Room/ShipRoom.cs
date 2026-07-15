using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class ShipRoom : MonoBehaviour, IRoom
    {
        [SerializeField] private string roomId;
        [SerializeField] private List<Transform> fireSpawnPoints = new List<Transform>();

        public string RoomId { get { return roomId; } }
        public IReadOnlyList<Transform> FireSpawnPoints { get { return fireSpawnPoints; } }

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