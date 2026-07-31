using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class ShipSpawnPoint : MonoBehaviour
    {
        [SerializeField] private List<ShipSpawnPoint> neighbors = new List<ShipSpawnPoint>();

        public IReadOnlyList<ShipSpawnPoint> Neighbors { get { return neighbors; } }

        public EventId? OccupiedBy { get; private set; }

        public bool IsFree { get { return OccupiedBy == null; } }

        public void Occupy(EventId eventId)
        {
            OccupiedBy = eventId;
        }

        public void Release()
        {
            OccupiedBy = null;
        }

        public void AddNeighbor(ShipSpawnPoint point)
        {
            if (point != this && !neighbors.Contains(point))
            {
                neighbors.Add(point);
            }
        }

        public void ClearNeighbors()
        {
            neighbors.Clear();
        }
    }
}