using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [CreateAssetMenu(
        fileName = "PHS_MapCatalog",
        menuName = "LastJumpCrew/ParkHanSol/Map Catalog")]
    public sealed class PHSMapCatalogSO : ScriptableObject, IMapProfileResolver
    {
        [SerializeField] private List<PHSMapProfileSO> profiles = new();

        private readonly Dictionary<int, PHSMapProfileSO> profilesById = new();
        private bool catalogValid;

        public IReadOnlyList<PHSMapProfileSO> Profiles => profiles;

        private void OnEnable()
        {
            RebuildIndex(true);
        }

        private void OnValidate()
        {
            RebuildIndex(true);
        }

        public bool TryResolve(int mapId, out PHSMapProfileSO profile)
        {
            profile = null;
            if (!catalogValid)
            {
                Debug.LogError(
                    $"PHS_MAP_RESOLVE_FAILED reason=catalog_invalid id={mapId} catalog={name}",
                    this);
                return false;
            }

            if (mapId < PHSMapProfileSO.MinimumMapId || mapId > PHSMapProfileSO.MaximumMapId)
            {
                Debug.LogError(
                    $"PHS_MAP_RESOLVE_FAILED reason=map_id_out_of_range id={mapId} required={PHSMapProfileSO.MinimumMapId}-{PHSMapProfileSO.MaximumMapId}",
                    this);
                return false;
            }

            if (!profilesById.TryGetValue(mapId, out profile))
            {
                Debug.LogError(
                    $"PHS_MAP_RESOLVE_FAILED reason=map_profile_not_found id={mapId} catalog={name}",
                    this);
                return false;
            }

            return true;
        }

        public bool TryValidate(out string reason)
        {
            return RebuildIndex(false, out reason);
        }

        private void RebuildIndex(bool logErrors)
        {
            catalogValid = RebuildIndex(logErrors, out _);
        }

        private bool RebuildIndex(bool logErrors, out string reason)
        {
            profilesById.Clear();
            if (profiles == null || profiles.Count == 0)
            {
                reason = "profiles_missing";
                LogInvalid(logErrors, reason);
                return false;
            }

            for (var index = 0; index < profiles.Count; index++)
            {
                var profile = profiles[index];
                if (profile == null)
                {
                    reason = $"profile_missing:index={index}";
                    profilesById.Clear();
                    LogInvalid(logErrors, reason);
                    return false;
                }

                if (!profile.TryValidate(out var profileReason))
                {
                    reason = $"profile_invalid:index={index}:asset={profile.name}:{profileReason}";
                    profilesById.Clear();
                    LogInvalid(logErrors, reason);
                    return false;
                }

                if (!profilesById.TryAdd(profile.MapId, profile))
                {
                    reason = $"profile_id_duplicate:id={profile.MapId}:asset={profile.name}";
                    profilesById.Clear();
                    LogInvalid(logErrors, reason);
                    return false;
                }
            }

            reason = null;
            catalogValid = true;
            return true;
        }

        private void LogInvalid(bool logErrors, string reason)
        {
            catalogValid = false;
            if (logErrors)
            {
                Debug.LogError($"PHS_MAP_CATALOG_INVALID asset={name} reason={reason}", this);
            }
        }
    }
}
