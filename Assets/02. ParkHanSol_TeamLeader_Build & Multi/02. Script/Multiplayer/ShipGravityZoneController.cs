using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ShipGravityZoneController : MonoBehaviour
    {
        [SerializeField] private NetworkPlayerGravityArea[] shipInteriorAreas;

        public void SetGravityEnabled(bool isEnabled)
        {
            if (shipInteriorAreas == null || shipInteriorAreas.Length == 0)
            {
                Debug.LogError($"PHS_SHIP_GRAVITY_SET_FAILED reason=areas_missing controller={name}");
                return;
            }

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

        public void TurnGravityOn()
        {
            SetGravityEnabled(true);
        }

        public void TurnGravityOff()
        {
            SetGravityEnabled(false);
        }
    }
}
