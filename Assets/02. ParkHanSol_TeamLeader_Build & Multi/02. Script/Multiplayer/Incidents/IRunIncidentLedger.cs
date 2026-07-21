using System;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IRunIncidentLedger
    {
        NetworkRunIncidentSnapshot Snapshot { get; }
        int CommandCount { get; }

        event Action<
            NetworkRunIncidentSnapshot,
            NetworkRunIncidentSnapshot> SnapshotChanged;
        event Action<NetworkListEvent<NetworkRunIncidentCommand>> CommandChanged;

        NetworkRunIncidentCommand GetCommandAt(int index);
        bool TryGetCommand(
            ulong commandId,
            out NetworkRunIncidentCommand command);

        bool TryBeginStageServer(
            int mapId,
            uint stageSequence,
            ushort pressureCapacity,
            byte maximumExternalCommands,
            byte maximumInternalCommands,
            out string reason);

        bool TryReserveCommandServer(
            in NetworkRunIncidentRequest request,
            out NetworkRunIncidentCommand command,
            out string reason);

        bool TryClaimCommandServer(
            ulong commandId,
            ulong executorNetworkObjectId,
            out NetworkRunIncidentCommand command,
            out string reason);

        bool TryActivateCommandServer(
            ulong commandId,
            ulong executorNetworkObjectId,
            ulong runtimeInstanceId,
            string targetId,
            out string reason);

        bool TryCompleteCommandServer(
            ulong commandId,
            ulong executorNetworkObjectId,
            bool succeeded,
            string outcomeId,
            out string reason);

        bool TryCancelCommandServer(
            ulong commandId,
            string cause,
            out string reason);

        bool TryCancelStageServer(
            uint stageSequence,
            string cause,
            out string reason);
    }
}
