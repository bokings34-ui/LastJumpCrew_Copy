using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class NetworkExteriorPortalDestination : MonoBehaviour
    {
        [SerializeField] private string destinationId;
        public string DestinationId => destinationId;
    }
}
