namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Wire-level content contract shared by authored trigger routes and
    /// incident command consumers.
    /// </summary>
    public static class IncidentRequestContentContract
    {
        public const int FireAccidentId = 1;
        public const int PowerFailureAccidentId = 2;
        public const int DeviceFailureAccidentId = 3;
        public const int HullBreachAccidentId = 4;
        public const int SteamLeakAccidentId = 5;
        public const int OxygenFailureAccidentId = 6;
        public const int GravityGeneratorFailureAccidentId = 7;

        public const int FireEventId = 7101;
        public const int PowerOffEventId = 7103;
        public const int EngineBreakEventId = 7105;
        public const int HullBreachEventId = 7107;
        public const int SteamLeakEventId = 7108;
        public const int OxygenGeneratorFailureEventId = 7109;
        public const int GravityGeneratorFailureEventId = 7110;

        public const int EnemyScoutEventId = 7201;
        public const int MeteorAttackEventId = 7202;
        public const int EmpAttackEventId = 7203;

        public static bool TryValidate(
            NetworkRunIncidentChannel channel,
            NetworkRunIncidentPayloadKind payloadKind,
            NetworkRunIncidentFamily incidentFamily,
            int contentId,
            out string reason)
        {
            if (!TryNormalize(
                    channel,
                    payloadKind,
                    contentId,
                    out payloadKind,
                    out contentId,
                    out reason))
            {
                return false;
            }

            switch (channel)
            {
                case NetworkRunIncidentChannel.External:
                    if (payloadKind
                        != NetworkRunIncidentPayloadKind.EventManagerEvent)
                    {
                        reason = "channel_payload_mismatch";
                        return false;
                    }

                    return TryValidateExternal(
                        incidentFamily,
                        contentId,
                        out reason);

                case NetworkRunIncidentChannel.Internal:
                    if (payloadKind
                        != NetworkRunIncidentPayloadKind.EventManagerEvent)
                    {
                        reason = "channel_payload_mismatch";
                        return false;
                    }

                    return TryValidateInternal(
                        incidentFamily,
                        contentId,
                        out reason);

                default:
                    reason = "channel_invalid";
                    return false;
            }
        }

        public static bool TryNormalize(
            NetworkRunIncidentChannel channel,
            NetworkRunIncidentPayloadKind payloadKind,
            int contentId,
            out NetworkRunIncidentPayloadKind normalizedPayloadKind,
            out int normalizedContentId,
            out string reason)
        {
            normalizedPayloadKind = payloadKind;
            normalizedContentId = contentId;
            reason = null;

            if (channel != NetworkRunIncidentChannel.Internal)
            {
                return true;
            }

            if (payloadKind == NetworkRunIncidentPayloadKind.EventManagerEvent)
            {
                return true;
            }

            if (payloadKind != NetworkRunIncidentPayloadKind.ShipAccident
                || !TryMapLegacyAccidentToEvent(contentId, out normalizedContentId))
            {
                reason = $"internal_content_mapping_missing:{contentId}";
                return false;
            }

            normalizedPayloadKind = NetworkRunIncidentPayloadKind.EventManagerEvent;
            return true;
        }

        public static bool TryMapLegacyAccidentToEvent(
            int legacyAccidentId,
            out int eventId)
        {
            switch (legacyAccidentId)
            {
                case FireAccidentId: eventId = FireEventId; return true;
                case PowerFailureAccidentId: eventId = PowerOffEventId; return true;
                case DeviceFailureAccidentId: eventId = EngineBreakEventId; return true;
                case HullBreachAccidentId: eventId = HullBreachEventId; return true;
                case SteamLeakAccidentId: eventId = SteamLeakEventId; return true;
                case OxygenFailureAccidentId: eventId = OxygenGeneratorFailureEventId; return true;
                case GravityGeneratorFailureAccidentId: eventId = GravityGeneratorFailureEventId; return true;
                default: eventId = 0; return false;
            }
        }

        public static bool TryMapEventToLegacyAccident(
            int eventId,
            out int legacyAccidentId)
        {
            switch (eventId)
            {
                case FireEventId: legacyAccidentId = FireAccidentId; return true;
                case PowerOffEventId: legacyAccidentId = PowerFailureAccidentId; return true;
                case EngineBreakEventId: legacyAccidentId = DeviceFailureAccidentId; return true;
                case HullBreachEventId: legacyAccidentId = HullBreachAccidentId; return true;
                case SteamLeakEventId: legacyAccidentId = SteamLeakAccidentId; return true;
                case OxygenGeneratorFailureEventId: legacyAccidentId = OxygenFailureAccidentId; return true;
                case GravityGeneratorFailureEventId: legacyAccidentId = GravityGeneratorFailureAccidentId; return true;
                default: legacyAccidentId = 0; return false;
            }
        }

        private static bool TryValidateExternal(
            NetworkRunIncidentFamily incidentFamily,
            int contentId,
            out string reason)
        {
            NetworkRunIncidentFamily expectedFamily;
            switch (contentId)
            {
                case EnemyScoutEventId:
                    expectedFamily = NetworkRunIncidentFamily.Enemy;
                    break;
                case MeteorAttackEventId:
                    expectedFamily = NetworkRunIncidentFamily.Meteor;
                    break;
                case EmpAttackEventId:
                    expectedFamily = NetworkRunIncidentFamily.EMP;
                    break;
                default:
                    reason = $"external_content_id_not_supported:{contentId}";
                    return false;
            }

            return TryValidateFamily(
                expectedFamily,
                incidentFamily,
                contentId,
                out reason);
        }

        private static bool TryValidateInternal(
            NetworkRunIncidentFamily incidentFamily,
            int contentId,
            out string reason)
        {
            NetworkRunIncidentFamily expectedFamily;
            switch (contentId)
            {
                case FireEventId:
                    expectedFamily = NetworkRunIncidentFamily.Fire;
                    break;
                case PowerOffEventId:
                    expectedFamily = NetworkRunIncidentFamily.Power;
                    break;
                case EngineBreakEventId:
                    expectedFamily = NetworkRunIncidentFamily.Device;
                    break;
                case HullBreachEventId:
                    expectedFamily = NetworkRunIncidentFamily.Hull;
                    break;
                case SteamLeakEventId:
                    expectedFamily = NetworkRunIncidentFamily.Steam;
                    break;
                case OxygenGeneratorFailureEventId:
                    expectedFamily = NetworkRunIncidentFamily.Oxygen;
                    break;
                case GravityGeneratorFailureEventId:
                    expectedFamily = NetworkRunIncidentFamily.Gravity;
                    break;
                default:
                    reason = $"internal_content_id_not_supported:{contentId}";
                    return false;
            }

            return TryValidateFamily(
                expectedFamily,
                incidentFamily,
                contentId,
                out reason);
        }

        private static bool TryValidateFamily(
            NetworkRunIncidentFamily expectedFamily,
            NetworkRunIncidentFamily incidentFamily,
            int contentId,
            out string reason)
        {
            if (incidentFamily != expectedFamily)
            {
                reason =
                    $"incident_family_mismatch:{contentId}:" +
                    $"expected={expectedFamily}:actual={incidentFamily}";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
