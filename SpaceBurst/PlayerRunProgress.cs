using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    sealed class PlayerRunProgress
    {
        public WeaponInventoryState Weapons { get; } = new WeaponInventoryState();
        public PowerupDropState Powerups { get; } = new PowerupDropState();

        public int StartingLives { get; private set; } = 3;
        public int ShipsPerLife { get; private set; } = 2;

        public void BeginCampaign(StageDefinition stage)
        {
            StartingLives = stage?.StartingLives > 0 ? stage.StartingLives : 3;
            ShipsPerLife = stage?.ShipsPerLife > 0 ? stage.ShipsPerLife : 2;
            Weapons.Reset();
            Powerups.Reset();
        }

        public void ApplyStageDefaults(StageDefinition stage)
        {
            if (stage == null)
                return;

            StartingLives = stage.StartingLives > 0 ? stage.StartingLives : StartingLives;
            ShipsPerLife = stage.ShipsPerLife > 0 ? stage.ShipsPerLife : ShipsPerLife;
        }
    }
}
