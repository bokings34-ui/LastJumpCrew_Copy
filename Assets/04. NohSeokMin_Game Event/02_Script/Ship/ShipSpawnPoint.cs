using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class ShipSpawnPoint : MonoBehaviour
    {
        [SerializeField] private List<ShipSpawnPoint> neighbors = new List<ShipSpawnPoint>();

        public IReadOnlyList<ShipSpawnPoint> Neighbors { get { return neighbors; } }

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