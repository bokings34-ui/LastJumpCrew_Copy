using UnityEngine.InputSystem;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolHudMotionTestDriver : MonoBehaviour
    {
        [SerializeField] private ParkHanSolHudTextMotion boosterTextMotion;
        [SerializeField] private ParkHanSolHudTextMotion healthTextMotion;
        [SerializeField] private ParkHanSolHudTextMotion shipHealthTextMotion;
        [SerializeField] private ParkHanSolHudGaugeMotion warpGaugeMotion;
        [SerializeField] private ParkHanSolHudTimerMotion timerMotion;
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(1)] private int maxFuel = 100;
        [SerializeField, Min(1)] private int maxShipHealth = 100;
        [SerializeField, Min(1)] private int maxWarpCharge = 100;
        [SerializeField, Min(1f)] private float timerDuration = 20f;
        [SerializeField, Min(1)] private int healthStep = 15;
        [SerializeField, Min(1)] private int fuelStep = 10;
        [SerializeField, Min(1)] private int shipHealthStep = 20;
        [SerializeField, Min(1)] private int warpChargeStep = 20;

        private int currentHealth;
        private int currentFuel;
        private int currentShipHealth;
        private int currentWarpCharge;
        private float currentTimer;

        private void Awake()
        {
            currentHealth = maxHealth;
            currentFuel = maxFuel;
            currentShipHealth = maxShipHealth;
            currentTimer = timerDuration;
            RefreshAll();
        }

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                ConsumeFuel();
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                RecoverFuel();
            }

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                ApplyDamage();
            }

            if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                RecoverHealth();
            }

            if (Keyboard.current.digit5Key.wasPressedThisFrame)
            {
                ApplyShipDamage();
            }

            if (Keyboard.current.digit6Key.wasPressedThisFrame)
            {
                ChargeWarp();
            }

            currentTimer = Mathf.Max(0f, currentTimer - Time.unscaledDeltaTime);
            timerMotion.SetTime(currentTimer, timerDuration);
        }

        private void ConsumeFuel()
        {
            var nextFuel = Mathf.Max(0, currentFuel - fuelStep);
            if (nextFuel == currentFuel)
            {
                boosterTextMotion.PlayDrainFeedback();
                return;
            }

            currentFuel = nextFuel;
            RefreshBooster();
            boosterTextMotion.PlayDrainFeedback();
        }

        private void RecoverFuel()
        {
            var nextFuel = Mathf.Min(maxFuel, currentFuel + fuelStep);
            if (nextFuel == currentFuel)
            {
                return;
            }

            currentFuel = nextFuel;
            RefreshBooster();
            boosterTextMotion.PlayIncreaseFeedback();
        }

        private void ApplyDamage()
        {
            currentHealth = Mathf.Max(0, currentHealth - healthStep);
            RefreshHealth();
            healthTextMotion.PlayDamageFeedback(false);
        }

        private void RecoverHealth()
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + healthStep);
            RefreshHealth();
            healthTextMotion.PlayIncreaseFeedback();
        }

        private void ApplyShipDamage()
        {
            currentShipHealth = Mathf.Max(0, currentShipHealth - shipHealthStep);
            RefreshShipHealth();
            shipHealthTextMotion.PlayDamageFeedback(true);
        }

        private void ChargeWarp()
        {
            var nextWarpCharge = Mathf.Min(maxWarpCharge, currentWarpCharge + warpChargeStep);
            if (nextWarpCharge == currentWarpCharge)
            {
                return;
            }

            currentWarpCharge = nextWarpCharge;
            warpGaugeMotion.SetValue((float)currentWarpCharge / maxWarpCharge);
            if (currentWarpCharge == maxWarpCharge)
            {
                warpGaugeMotion.PlayFullFeedback();
            }
        }

        private void RefreshAll()
        {
            RefreshBooster();
            RefreshHealth();
            RefreshShipHealth();
            warpGaugeMotion.SetValue(0f);
            timerMotion.SetTime(currentTimer, timerDuration);
        }

        private void RefreshBooster()
        {
            boosterTextMotion.SetValue($"BOOST {currentFuel}<size=24>/{maxFuel}</size>", (float)currentFuel / maxFuel);
        }

        private void RefreshHealth()
        {
            healthTextMotion.SetValue($"+{currentHealth}<size=26>/{maxHealth}</size>", (float)currentHealth / maxHealth);
        }

        private void RefreshShipHealth()
        {
            shipHealthTextMotion.SetValue($"SHIP HP {currentShipHealth}<size=24>/{maxShipHealth}</size>", (float)currentShipHealth / maxShipHealth);
        }
    }
}
