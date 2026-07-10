using UnityEngine;

namespace SM
{
    public class TestDummy : MonoBehaviour
    {
        private void OnEnable() { PlayerRegistry.Instance.SetPlayer(transform); }
        private void OnDisable() { PlayerRegistry.Peek()?.ClearPlayer(transform); }

    }
}