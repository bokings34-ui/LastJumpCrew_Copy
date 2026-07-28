namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    public enum NetworkAudioCue : byte
    {
        ItemPickup = 0,
        ItemSwap = 1,
        ItemDrop = 2,
        ShopSuccess = 3,
        ShopFailure = 4,
        Warning = 5,
        RunClear = 6,
        RunGameOver = 7,
        RestartRequested = 8,
        RestartSucceeded = 9,
        RestartFailed = 10,
        TutorialComplete = 11,
        WrenchImpact = 12,
        RepairComplete = 13,
        ExtinguisherSpray = 14,
        ExtinguishComplete = 15,
        BatteryInstall = 16,
        FoamShot = 17,
        FoamAttach = 18,
        FoamHarden = 19,
        FoamSealComplete = 20,
        FoamFireComplete = 21,
        DebrisDeposit = 22,
        FootstepWalk = 23,
        FootstepRun = 24,
        PlayerJump = 25,
        MissionSuccess = 26,
        VendingInteraction = 27,
        InteractionFocus = 28,
        OptionsSaved = 29,
        WarpStart = 30,
        WarpEnd = 31,
        AccidentAppeared = 32
    }
}
