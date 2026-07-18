using System;
using Unity.Collections;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public struct NetworkRunIncidentCommand :
        INetworkSerializable,
        IEquatable<NetworkRunIncidentCommand>
    {
        public ulong CommandId;
        public FixedString64Bytes RequestId;
        public ulong ParentCommandId;
        public uint StageSequence;
        public int MapId;
        public NetworkRunIncidentChannel Channel;
        public NetworkRunIncidentPayloadKind PayloadKind;
        public NetworkRunIncidentFamily IncidentFamily;
        public int ContentId;
        public NetworkRunIncidentSourceKind SourceKind;
        public ushort PressureCost;
        public float WarpChargeMultiplier;
        public NetworkRunIncidentCommandState State;
        public ulong ExecutorNetworkObjectId;
        public ulong RuntimeInstanceId;
        public FixedString64Bytes TargetId;
        public FixedString64Bytes OutcomeId;
        public FixedString64Bytes CancelReason;
        public uint Revision;
        public uint StateRevision;
        public double ChangedAtServerTime;

        public NetworkRunIncidentCommand(
            ulong commandId,
            FixedString64Bytes requestId,
            ulong parentCommandId,
            uint stageSequence,
            int mapId,
            NetworkRunIncidentChannel channel,
            NetworkRunIncidentPayloadKind payloadKind,
            NetworkRunIncidentFamily incidentFamily,
            int contentId,
            NetworkRunIncidentSourceKind sourceKind,
            ushort pressureCost,
            float warpChargeMultiplier,
            NetworkRunIncidentCommandState state,
            ulong executorNetworkObjectId,
            ulong runtimeInstanceId,
            FixedString64Bytes targetId,
            FixedString64Bytes outcomeId,
            FixedString64Bytes cancelReason,
            uint revision,
            uint stateRevision,
            double changedAtServerTime)
        {
            CommandId = commandId;
            RequestId = requestId;
            ParentCommandId = parentCommandId;
            StageSequence = stageSequence;
            MapId = mapId;
            Channel = channel;
            PayloadKind = payloadKind;
            IncidentFamily = incidentFamily;
            ContentId = contentId;
            SourceKind = sourceKind;
            PressureCost = pressureCost;
            WarpChargeMultiplier = warpChargeMultiplier;
            State = state;
            ExecutorNetworkObjectId = executorNetworkObjectId;
            RuntimeInstanceId = runtimeInstanceId;
            TargetId = targetId;
            OutcomeId = outcomeId;
            CancelReason = cancelReason;
            Revision = revision;
            StateRevision = stateRevision;
            ChangedAtServerTime = changedAtServerTime;
        }

        public bool IsTerminal =>
            State == NetworkRunIncidentCommandState.Resolved
            || State == NetworkRunIncidentCommandState.Failed
            || State == NetworkRunIncidentCommandState.Cancelled;

        public bool HoldsReservedPressure =>
            State == NetworkRunIncidentCommandState.Pending
            || State == NetworkRunIncidentCommandState.Claimed;

        public bool HoldsActivePressure =>
            State == NetworkRunIncidentCommandState.Active;

        public NetworkRunIncidentCommand WithState(
            NetworkRunIncidentCommandState state,
            ulong executorNetworkObjectId,
            ulong runtimeInstanceId,
            FixedString64Bytes targetId,
            FixedString64Bytes outcomeId,
            FixedString64Bytes cancelReason,
            uint stateRevision,
            double changedAtServerTime)
        {
            return new NetworkRunIncidentCommand(
                CommandId,
                RequestId,
                ParentCommandId,
                StageSequence,
                MapId,
                Channel,
                PayloadKind,
                IncidentFamily,
                ContentId,
                SourceKind,
                PressureCost,
                WarpChargeMultiplier,
                state,
                executorNetworkObjectId,
                runtimeInstanceId,
                targetId,
                outcomeId,
                cancelReason,
                Revision,
                stateRevision,
                changedAtServerTime);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref CommandId);
            serializer.SerializeValue(ref RequestId);
            serializer.SerializeValue(ref ParentCommandId);
            serializer.SerializeValue(ref StageSequence);
            serializer.SerializeValue(ref MapId);
            serializer.SerializeValue(ref Channel);
            serializer.SerializeValue(ref PayloadKind);
            serializer.SerializeValue(ref IncidentFamily);
            serializer.SerializeValue(ref ContentId);
            serializer.SerializeValue(ref SourceKind);
            serializer.SerializeValue(ref PressureCost);
            serializer.SerializeValue(ref WarpChargeMultiplier);
            serializer.SerializeValue(ref State);
            serializer.SerializeValue(ref ExecutorNetworkObjectId);
            serializer.SerializeValue(ref RuntimeInstanceId);
            serializer.SerializeValue(ref TargetId);
            serializer.SerializeValue(ref OutcomeId);
            serializer.SerializeValue(ref CancelReason);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref StateRevision);
            serializer.SerializeValue(ref ChangedAtServerTime);
        }

        public bool Equals(NetworkRunIncidentCommand other)
        {
            return CommandId == other.CommandId
                && RequestId.Equals(other.RequestId)
                && ParentCommandId == other.ParentCommandId
                && StageSequence == other.StageSequence
                && MapId == other.MapId
                && Channel == other.Channel
                && PayloadKind == other.PayloadKind
                && IncidentFamily == other.IncidentFamily
                && ContentId == other.ContentId
                && SourceKind == other.SourceKind
                && PressureCost == other.PressureCost
                && WarpChargeMultiplier.Equals(other.WarpChargeMultiplier)
                && State == other.State
                && ExecutorNetworkObjectId == other.ExecutorNetworkObjectId
                && RuntimeInstanceId == other.RuntimeInstanceId
                && TargetId.Equals(other.TargetId)
                && OutcomeId.Equals(other.OutcomeId)
                && CancelReason.Equals(other.CancelReason)
                && Revision == other.Revision
                && StateRevision == other.StateRevision
                && ChangedAtServerTime.Equals(other.ChangedAtServerTime);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkRunIncidentCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CommandId.GetHashCode();
                hash = (hash * 397) ^ RequestId.GetHashCode();
                hash = (hash * 397) ^ ParentCommandId.GetHashCode();
                hash = (hash * 397) ^ (int)StageSequence;
                hash = (hash * 397) ^ MapId;
                hash = (hash * 397) ^ (byte)Channel;
                hash = (hash * 397) ^ (byte)PayloadKind;
                hash = (hash * 397) ^ (byte)IncidentFamily;
                hash = (hash * 397) ^ ContentId;
                hash = (hash * 397) ^ (byte)SourceKind;
                hash = (hash * 397) ^ PressureCost;
                hash = (hash * 397) ^ WarpChargeMultiplier.GetHashCode();
                hash = (hash * 397) ^ (byte)State;
                hash = (hash * 397) ^ ExecutorNetworkObjectId.GetHashCode();
                hash = (hash * 397) ^ RuntimeInstanceId.GetHashCode();
                hash = (hash * 397) ^ TargetId.GetHashCode();
                hash = (hash * 397) ^ OutcomeId.GetHashCode();
                hash = (hash * 397) ^ CancelReason.GetHashCode();
                hash = (hash * 397) ^ (int)Revision;
                hash = (hash * 397) ^ (int)StateRevision;
                hash = (hash * 397) ^ ChangedAtServerTime.GetHashCode();
                return hash;
            }
        }
    }
}
