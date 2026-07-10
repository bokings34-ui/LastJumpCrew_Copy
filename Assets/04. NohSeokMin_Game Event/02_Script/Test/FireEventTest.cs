using UnityEngine;
using UnityEngine.InputSystem;

namespace SM
{
    public class FireEventTest : MonoBehaviour
    {
        [SerializeField] private TestRoom room;

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                EventManager.Instance.SpawnEvent(EventId.Fire, room);
            }

            if (Keyboard.current.wKey.wasPressedThisFrame)
            {
                var target = FindAnyObjectByType<FireEffectInstance>();
                //target?.ApplyRepair(10f);
            }
        }
    }
}