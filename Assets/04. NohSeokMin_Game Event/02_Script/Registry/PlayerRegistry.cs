using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class PlayerRegistry : MonoSingleton<PlayerRegistry>
    {
        private readonly List<Transform> _players = new List<Transform>();

        public void Register(Transform player)
        {
            if (!_players.Contains(player)) _players.Add(player);
        }

        public void Unregister(Transform player)
        {
            _players.Remove(player);
        }

        public Transform GetNearestPlayer(Vector3 fromPosition)
        {
            Transform closest = null;
            float closestDist = float.MaxValue;

            foreach (var player in _players)
            {
                if (player == null) continue;

                float dist = Vector3.Distance(fromPosition, player.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = player;
                }
            }

            return closest;
        }
    }
}