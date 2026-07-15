using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    // TODO :: Player 에 붙을 코드 요청할 것
    // private void OnEnable() { PlayerRegistry.Instance.Register(transform); }
    // private void OnDisable() { PlayerRegistry.Peek()?.Unregister(transform); }

    // TODO :: Device 에 붙을 코드 요청할 것
    // public Transform Transform => transform;
    // private void OnEnable() { DeviceRegistry.Instance.Register(this); }
    // private void OnDisable() { DeviceRegistry.Peek()?.Unregister(this); }

    // TODO :: Room 만들 때 붙혀놓으면 스스로 등록/해제 함. 팀원에게 요청할 것
    // IRoom 인터페이스 참조 / Room Id , FireSpawnPoints
    // private void OnEnable() { RoomRegistry.Instance.Register(this); }
    // private void OnDisable() { RoomRegistry.Instance.Unregister(this); }
    public interface IRoom
    {
        string RoomId { get; }
        IReadOnlyList<Transform> FireSpawnPoints { get; }
    }
    
    public interface IDevice
    {
        Transform Transform { get; }
    }

    public interface IEventSpawner
    {
        bool SpawnEvent(EventId id, IRoom targetRoom, Action<EventBase, bool> onFinished = null);
    }
}
