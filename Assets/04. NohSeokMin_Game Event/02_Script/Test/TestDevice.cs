using SM;
using UnityEngine;

public class TestDevice : MonoBehaviour, IDevice
{
    public Transform Transform => transform;

    private void OnEnable() { DeviceRegistry.Instance.Register(this); }
    private void OnDisable() { DeviceRegistry.Peek()?.Unregister(this); }

}
