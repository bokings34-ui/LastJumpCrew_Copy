using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class RoomRegistry : MonoSingleton<RoomRegistry>
    {
        private readonly List<IRoom> _rooms = new List<IRoom>();

        public void Register(IRoom room)
        {
            if (room == null || _rooms.Contains(room))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(room.RoomId))
            {
                Debug.LogError("[RoomRegistry] RoomId is empty.");
                return;
            }

            foreach (var registeredRoom in _rooms)
            {
                if (registeredRoom != null && registeredRoom.RoomId == room.RoomId)
                {
                    Debug.LogError($"[RoomRegistry] Duplicate RoomId: {room.RoomId}");
                    return;
                }
            }

            _rooms.Add(room);
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

        public bool TryGetRoom(string roomId, out IRoom room)
        {
            foreach (var registeredRoom in _rooms)
            {
                if (registeredRoom != null && registeredRoom.RoomId == roomId)
                {
                    room = registeredRoom;
                    return true;
                }
            }

            room = null;
            return false;
        }
    }
}
