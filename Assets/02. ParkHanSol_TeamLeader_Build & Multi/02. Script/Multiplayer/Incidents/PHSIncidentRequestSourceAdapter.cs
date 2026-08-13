using UnityEngine;

#if false // Removed from the team event runtime; retained only as historical source.
namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Inspector bridge for teammate trigger UnityEvents. It forwards one local
    /// signal to the server-only gateway and owns no scheduling or spawn logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PHSIncidentRequestSourceAdapter :
        MonoBehaviour,
        IIncidentRequestSource
    {
        [Header("Inspector References")]
        [SerializeField] private PHSIncidentRequestGateway gateway;

        [Header("Stable IDs")]
        [SerializeField] private string incidentSourceId;
        [SerializeField] private string incidentTargetId;

        [Header("PHS Wiring Only")]
        [SerializeField] private ulong parentCommandId;

        public string IncidentSourceId => incidentSourceId;
        public string IncidentTargetId => incidentTargetId;
        public PHSIncidentRequestGateway Gateway => gateway;

        public void RequestIncident()
        {
            if (!TryRequestIncident(out _, out var reason))
            {
                Debug.LogWarning(
                    $"PHS_INCIDENT_SOURCE_REJECTED source={incidentSourceId} " +
                    $"target={incidentTargetId} reason={reason}",
                    this);
            }
        }

        public bool TryRequestIncident(
            out NetworkRunIncidentCommand command,
            out string reason)
        {
            command = default;
            if (gateway == null)
            {
                reason = "request_gateway_missing";
                return false;
            }

            return gateway.TrySubmitServer(
                this,
                parentCommandId,
                out command,
                out reason);
        }
    }
}
#endif
