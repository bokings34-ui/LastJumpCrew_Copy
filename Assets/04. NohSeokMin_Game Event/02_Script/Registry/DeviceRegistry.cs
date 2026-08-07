using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SM
{
    public class DeviceRegistry : MonoSingleton<DeviceRegistry>
    {
        private readonly List<IDevice> _devices = new List<IDevice>();

        public void Register(IDevice device)
        {
            if (!_devices.Contains(device)) _devices.Add(device);
        }

        public void Unregister(IDevice device)
        {
            _devices.Remove(device);
        }

        public Transform GetNearestDeviceTransform(Vector3 fromPosition)
        {
            Transform closest = null;
            float closestDist = float.MaxValue;
            if (!NavMesh.SamplePosition(
                    fromPosition,
                    out var sourceHit,
                    3f,
                    NavMesh.AllAreas))
            {
                return null;
            }

            var path = new NavMeshPath();

            foreach (var device in _devices)
            {
                if (device == null
                    || device.Transform == null
                    || !device.Transform.gameObject.activeInHierarchy
                    || !NavMesh.SamplePosition(
                        device.Transform.position,
                        out var targetHit,
                        3f,
                        NavMesh.AllAreas)
                    || !NavMesh.CalculatePath(
                        sourceHit.position,
                        targetHit.position,
                        NavMesh.AllAreas,
                        path)
                    || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float dist = GetPathLength(path);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = device.Transform;
                }
            }

            return closest;
        }

        private static float GetPathLength(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2)
            {
                return float.MaxValue;
            }

            var length = 0f;
            for (var index = 1; index < path.corners.Length; index++)
            {
                length += Vector3.Distance(
                    path.corners[index - 1],
                    path.corners[index]);
            }

            return length;
        }
    }
}
