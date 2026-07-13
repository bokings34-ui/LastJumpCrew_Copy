using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ShipGravityZoneController : MonoBehaviour
    {
        [SerializeField] private NetworkPlayerGravityArea[] shipInteriorAreas;
        [SerializeField] private GravityZone[] gravityZones;

        public event Action<bool> GravityStateChanged;

        public bool IsGravityEnabled { get; private set; } = true;

        public void SetGravityEnabled(bool isEnabled)
        {
            var hasLegacyAreas = shipInteriorAreas != null && shipInteriorAreas.Length > 0;
            var hasGravityZones = gravityZones != null && gravityZones.Length > 0;
            if (!hasLegacyAreas && !hasGravityZones)
            {
                Debug.LogError($"PHS_SHIP_GRAVITY_SET_FAILED reason=areas_missing controller={name}");
                return;
            }

            if (hasLegacyAreas)
            {
                foreach (var area in shipInteriorAreas)
                {
                    if (area == null)
                    {
                        Debug.LogError($"PHS_SHIP_GRAVITY_SET_FAILED reason=area_missing controller={name}");
                        continue;
                    }

                    area.SetShipGravityEnabled(isEnabled);
                }
            }

            if (!hasGravityZones)
            {
                PublishGravityState(isEnabled);
                return;
            }

            foreach (var zone in gravityZones)
            {
                if (zone == null)
                {
                    Debug.LogError($"PHS_SHIP_GRAVITY_SET_FAILED reason=zone_missing controller={name}");
                    continue;
                }

                zone.SetShipGravityEnabled(isEnabled);
            }

            PublishGravityState(isEnabled);
        }

        public void TurnGravityOn()
        {
            SetGravityEnabled(true);
        }

        public void TurnGravityOff()
        {
            SetGravityEnabled(false);
        }

        private void PublishGravityState(bool isEnabled)
        {
            if (IsGravityEnabled == isEnabled)
            {
                return;
            }

            IsGravityEnabled = isEnabled;
            GravityStateChanged?.Invoke(isEnabled);
        }
    }
}
