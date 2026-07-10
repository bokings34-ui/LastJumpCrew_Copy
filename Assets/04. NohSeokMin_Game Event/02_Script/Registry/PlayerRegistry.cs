using UnityEngine;

namespace SM
{
    public class PlayerRegistry : MonoSingleton<PlayerRegistry>
    {
        // TODO :: Player 에 붙을 코드 요청할 것
        //private void OnEnable() { PlayerRegistry.Instance.SetPlayer(transform); }
        //private void OnDisable() { PlayerRegistry.Peek()?.ClearPlayer(transform); }
        public Transform PlayerTransform { get; private set; }

        public void SetPlayer(Transform player)
        {
            PlayerTransform = player;
        }

        public void ClearPlayer(Transform player)
        {
            if (PlayerTransform == player)
                PlayerTransform = null;
        }
    }
}