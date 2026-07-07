using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public interface IRepairable
    {
        float RepairProgress { get; }
        bool IsRepaired { get; }
        void ApplyRepair(float amount);
    }
    public interface IRoom
    {
        string RoomId { get; }
        IReadOnlyList<Transform> FireSpawnPoints { get; }
    }

    // IMiniGameTarget 결과 확정 시 받아올 것
    public interface IMinigameResult
    {
        void MinigameResult(bool success);
    }

    public interface IEventSpawner
    {
        void SpawnEvent(EventId id, IRoom targetRoom);
    }
    
    public interface IDamageable
    {
        void TakeDamage(float amount);
    }
}