using System;

namespace SpaceBurst
{
    sealed class PowerupDropState
    {
        private const float BaseChance = 0.12f;
        private const float BonusPerMiss = 0.04f;
        private const int GuaranteedOnKillCount = 6;

        public int EligibleKillsSinceLastDrop { get; private set; }

        public float CurrentChance
        {
            get { return MathF.Min(1f, BaseChance + EligibleKillsSinceLastDrop * BonusPerMiss); }
        }

        public float PityMeter
        {
            get { return MathF.Min(1f, EligibleKillsSinceLastDrop / (float)(GuaranteedOnKillCount - 1)); }
        }

        public void Reset()
        {
            EligibleKillsSinceLastDrop = 0;
        }

        public bool ShouldDrop(Random random, float sectionBonusChance, float weightMultiplier, bool guaranteed)
        {
            if (guaranteed)
            {
                EligibleKillsSinceLastDrop = 0;
                return true;
            }

            if (EligibleKillsSinceLastDrop >= GuaranteedOnKillCount - 1)
            {
                EligibleKillsSinceLastDrop = 0;
                return true;
            }

            float chance = MathF.Min(1f, CurrentChance + sectionBonusChance);
            chance *= MathF.Max(0f, weightMultiplier);
            bool drop = random.NextDouble() < chance;
            EligibleKillsSinceLastDrop = drop ? 0 : EligibleKillsSinceLastDrop + 1;
            return drop;
        }
    }
}
