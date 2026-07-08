using UnityEngine;
using System.Collections.Generic;

namespace SM
{
    public class TestRoom : MonoBehaviour, IRoom
    {
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

        private void OnEnable() { RoomRegistry.Instance.Register(this); }
        private void OnDisable() { RoomRegistry.Instance.Unregister(this); }

        public string RoomId => "TestRoom";
        public IReadOnlyList<Transform> FireSpawnPoints => spawnPoints;
    }
}