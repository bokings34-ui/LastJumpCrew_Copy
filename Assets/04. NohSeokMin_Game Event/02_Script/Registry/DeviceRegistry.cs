using System.Collections.Generic;
using UnityEngine;

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

            foreach (var device in _devices)
            {
                float dist = Vector3.Distance(fromPosition, device.Transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = device.Transform;
                }
            }

            return closest;
        }
    }
}