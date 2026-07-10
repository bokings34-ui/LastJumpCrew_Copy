using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public interface IRoom
    {
        string RoomId { get; }
        IReadOnlyList<Transform> FireSpawnPoints { get; }
    }

    // public Transform Transform => transform;
    // private void OnEnable() { PlayerRegistry.Instance.SetPlayer(transform); }
    // private void OnDisable() { PlayerRegistry.Peek()?.ClearPlayer(transform); }

    // public Transform Transform => transform;
    // private void OnEnable() { DeviceRegistry.Instance.Register(this); }
    // private void OnDisable() { DeviceRegistry.Peek()?.Unregister(this); }
    public interface IDevice
    {
        Transform Transform { get; }
    }

    public interface IEventSpawner
    {
        void SpawnEvent(EventId id, IRoom targetRoom, Action<EventBase, bool> onFinished = null);
    }
}