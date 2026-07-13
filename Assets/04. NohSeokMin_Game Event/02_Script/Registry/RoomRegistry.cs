using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class RoomRegistry : MonoSingleton<RoomRegistry>
    {
        private readonly List<IRoom> _rooms = new List<IRoom>();

        public void Register(IRoom room)
        {
            if (!_rooms.Contains(room)) _rooms.Add(room);
        }

        public void Unregister(IRoom room)
        {
            _rooms.Remove(room);
        }

        public IRoom GetRandomRoom()
        {
            if (_rooms.Count == 0) return null;
            return _rooms[Random.Range(0, _rooms.Count)];
        }
    }
}