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

        public bool Contains(Transform player)
        {
            return _players.Contains(player);
        }

        public Transform GetRandomActivePlayer()
        {
            var activeCount = 0;
            foreach (var player in _players)
            {
                if (player != null && player.gameObject.activeInHierarchy)
                {
                    activeCount++;
                }
            }

            if (activeCount == 0)
            {
                return null;
            }

            var selectedIndex = Random.Range(0, activeCount);
            foreach (var player in _players)
            {
                if (player == null || !player.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (selectedIndex-- == 0)
                {
                    return player;
                }
            }

            return null;
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
