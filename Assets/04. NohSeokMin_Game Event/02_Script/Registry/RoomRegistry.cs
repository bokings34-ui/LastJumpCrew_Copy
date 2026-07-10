using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class RoomRegistry : MonoSingleton<RoomRegistry>
    {
        private readonly List<IRoom> _rooms = new List<IRoom>();

        // TODO :: Room 만들 때 붙혀놓으면 스스로 등록/해제 함. 팀원에게 요청할 것
        // IRoom Interface 와 밑에 두줄
        // private void OnEnable() { RoomRegistry.Instance.Register(this); }
        // private void OnDisable() { RoomRegistry.Instance.Unregister(this); }

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