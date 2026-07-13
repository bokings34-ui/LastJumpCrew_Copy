namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IHudFeedback
    {
        void SetVitals(int health, int maxHealth, int stamina, int maxStamina);
        void SetThrusterFuel(int currentFuel, int maxFuel);
        void SetEconomy(int money, int bank);
        void SetWarpGauge(float normalizedValue);
        void SetShipHp(int current, int max);
        void SetTimeLimit(float seconds);
        void PlayHeldItemChanged(bool hasItem);
        void SetInteractionPrompt(string inputLabel, string prompt);
        void SetGravityWarning(bool isVisible);
    }
}
