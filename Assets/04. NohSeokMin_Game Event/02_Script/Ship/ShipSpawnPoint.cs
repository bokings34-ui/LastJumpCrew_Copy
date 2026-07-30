using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class ShipSpawnPoint : MonoBehaviour
    {
        [SerializeField] private List<ShipSpawnPoint> neighbors = new List<ShipSpawnPoint>();

        public IReadOnlyList<ShipSpawnPoint> Neighbors => neighbors;

        private EventId? _occupiedBy;
        public bool IsFree => _occupiedBy == null;
        public EventId? OccupiedBy => _occupiedBy;

        public void Occupy(EventId eventId)
        {
            _occupiedBy = eventId;
        }

        public void Release()
        {
            _occupiedBy = null;
        }

        public void AddNeighbor(ShipSpawnPoint point)
        {
            if (point != this && !neighbors.Contains(point))
                neighbors.Add(point);
        }

        public void ClearNeighbors()
        {
            neighbors.Clear();
        }
    }
}