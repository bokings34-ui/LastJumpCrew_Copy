using System;
using Unity.Collections;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public struct NetworkRunIncidentRequest :
        INetworkSerializable,
        IEquatable<NetworkRunIncidentRequest>
    {
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
        public FixedString64Bytes TargetId;

        public NetworkRunIncidentRequest(
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
            FixedString64Bytes targetId)
        {
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
            TargetId = targetId;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
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
            serializer.SerializeValue(ref TargetId);
        }

        public bool Equals(NetworkRunIncidentRequest other)
        {
            return RequestId.Equals(other.RequestId)
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
                && TargetId.Equals(other.TargetId);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkRunIncidentRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = RequestId.GetHashCode();
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
                hash = (hash * 397) ^ TargetId.GetHashCode();
                return hash;
            }
        }
    }
}
