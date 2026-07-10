using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class DebrisItem : MonoBehaviour
    {
        [SerializeField, Min(1)] private int value = 1;

        public int Value => value;
    }
}
