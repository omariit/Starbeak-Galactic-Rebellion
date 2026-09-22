namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Global tunable constants. Values are expressed in world units / seconds at 1080x1920 portrait reference.
    /// </summary>
    public static class GameConstants
    {
        // ---------------------------------------------------------------- Frame & performance
        public const int TargetFrameRate = 60;
        public const float FixedDeltaTime = 0.0167f;

        // ---------------------------------------------------------------- Reference resolution
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        // ---------------------------------------------------------------- Sector map generation
        public const int TierCount = 5;
        public const int EntryTier = 0;
        public const int BossTier = 4;
        public const int MinBranchesPerTier = 2;
        public const int MaxBranchesPerTier = 4;
        public const float BossHealthModifier = 12f;

        // ---------------------------------------------------------------- Flock mind matrix
        public const float FlockScanInterval = 0.25f;
        public const float WideSpreadThresholdDeg = 30f;
        public const float ArmoredColumnHealthBonus = 0.5f;
        public const float BeamSideStepOffset = 150f;

        // ---------------------------------------------------------------- Elite weapon matrix
        public const float NapalmHazardDuration = 3f;
        public const float MagneticGravitationalConstant = 9000f;
        public const int FlakShrapnelCount = 8;
        public const float FlakShrapnelSpreadDeg = 360f / FlakShrapnelCount;

        // ---------------------------------------------------------------- Status effects
        public const float FreezeSlowMultiplier = 0.6f;
        public const float FreezeDuration = 2f;

        // ---------------------------------------------------------------- Crafting table
        public const float MuzzleDamageMultiplier = 1.25f;
        public const float MuzzleSpreadOffset = -15f;
        public const float MagazineCooldownMultiplier = 1.10f;
        public const float CoreShieldLeechProbability = 0.05f;
        public const float CoreShieldLeechRatio = 0.05f;

        // ---------------------------------------------------------------- Object pooling budgets
        public const int PlayerProjectileBudget = 120;
        public const int EnemyProjectileBudget = 160;
        public const int EnemyBudget = 50;
        public const int EffectBudget = 40;

        // ---------------------------------------------------------------- Persistence
        public const string ProfileFileName = "usr_profile.dat";
    }
}
