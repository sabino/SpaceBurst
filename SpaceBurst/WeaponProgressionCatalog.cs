using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceBurst
{
    sealed class WeaponEvolutionDefinition
    {
        public EvolutionId Id { get; init; }
        public WeaponStyleId StyleId { get; init; }
        public PassiveReactorId RequiredPassive { get; init; }
        public int RequiredLevel { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string PreviewText { get; init; } = string.Empty;
        public string DeltaText { get; init; } = string.Empty;
    }

    static class WeaponProgressionCatalog
    {
        private static readonly Dictionary<WeaponStyleId, int> unlockStages = new Dictionary<WeaponStyleId, int>
        {
            [WeaponStyleId.Pulse] = 1,
            [WeaponStyleId.Spread] = 1,
            [WeaponStyleId.Missile] = 1,
            [WeaponStyleId.Laser] = 11,
            [WeaponStyleId.Arc] = 11,
            [WeaponStyleId.Plasma] = 21,
            [WeaponStyleId.Drone] = 21,
            [WeaponStyleId.Rail] = 31,
            [WeaponStyleId.Blade] = 31,
            [WeaponStyleId.Fortress] = 41,
        };

        private static readonly IReadOnlyList<WeaponEvolutionDefinition> evolutions = new[]
        {
            Evolution(EvolutionId.SingularityRail, WeaponStyleId.Pulse, PassiveReactorId.Overclock, 3,
                "SINGULARITY RAIL", "CORE PULSE COLLAPSES INTO A PIERCING HYPER-RAIL", "PULSE -> RAIL", "PIERCE + DMG"),
            Evolution(EvolutionId.NovaFan, WeaponStyleId.Spread, PassiveReactorId.MagnetCore, 3,
                "NOVA FAN", "SPREAD SHELLS BLOOM INTO A GRAVITY-WARPED STAR FAN", "SPREAD -> NOVA", "PELLETS + WIDTH"),
            Evolution(EvolutionId.PrismLance, WeaponStyleId.Laser, PassiveReactorId.ChainReactor, 3,
                "PRISM LANCE", "LASER SPLITS INTO LINKED LANCES THAT CARVE THE WHOLE LANE", "LASER -> PRISM", "BEAMS + TICKS"),
            Evolution(EvolutionId.ChronoNova, WeaponStyleId.Plasma, PassiveReactorId.TimeBattery, 3,
                "CHRONO NOVA", "PLASMA ORBS LEAVE TIME-SHEARED DETONATION FIELDS", "PLASMA -> NOVA", "BLAST + ORB"),
            Evolution(EvolutionId.CataclysmRack, WeaponStyleId.Missile, PassiveReactorId.SalvageNode, 2,
                "CATACLYSM RACK", "MISSILES SPLIT HARDER AND DETONATE WIDER", "MISSILE ++", "BLAST + VOLLEY"),
            Evolution(EvolutionId.VoidLance, WeaponStyleId.Rail, PassiveReactorId.Overclock, 3,
                "VOID LANCE", "RAIL SLUGS PUNCH THROUGH FORMATIONS WITH EXTREME VELOCITY", "RAIL -> VOID", "PIERCE + SPEED"),
            Evolution(EvolutionId.TempestCircuit, WeaponStyleId.Arc, PassiveReactorId.ChainReactor, 3,
                "TEMPEST CIRCUIT", "ARC BOLTS FORK INTO A SELF-FEEDING LIGHTNING WEB", "ARC -> TEMPEST", "CHAIN + FORK"),
            Evolution(EvolutionId.AegisStorm, WeaponStyleId.Blade, PassiveReactorId.ArmorPlating, 3,
                "AEGIS STORM", "BLADE WAVES FORM A PIERCING DEFENSIVE STORM", "BLADE -> AEGIS", "WAVES + PIERCE"),
            Evolution(EvolutionId.EchoHive, WeaponStyleId.Drone, PassiveReactorId.TimeBattery, 2,
                "ECHO HIVE", "DRONES MULTIPLY AND FIRE THROUGH REWIND AFTERGLOWS", "DRONE ++", "DRONES + CHAIN"),
            Evolution(EvolutionId.CitadelNova, WeaponStyleId.Fortress, PassiveReactorId.ArmorPlating, 3,
                "CITADEL NOVA", "FORTRESS PULSES BECOME A LAYERED EXPLOSIVE BULWARK", "FORTRESS -> NOVA", "BURST + BLAST"),
        };

        public static IReadOnlyList<WeaponEvolutionDefinition> Evolutions
        {
            get { return evolutions; }
        }

        public static int GetUnlockStage(WeaponStyleId styleId)
        {
            return unlockStages.TryGetValue(styleId, out int stageNumber) ? stageNumber : 50;
        }

        public static bool IsAvailableAtStage(WeaponStyleId styleId, int stageNumber)
        {
            return GetUnlockStage(styleId) <= Math.Clamp(stageNumber, 1, 50);
        }

        public static IReadOnlyList<WeaponStyleId> GetAvailableStyles(int stageNumber)
        {
            return WeaponCatalog.StyleOrder.Where(style => IsAvailableAtStage(style, stageNumber)).ToList();
        }

        public static WeaponEvolutionDefinition GetEvolution(EvolutionId evolutionId)
        {
            return evolutions.FirstOrDefault(definition => definition.Id == evolutionId);
        }

        public static WeaponEvolutionDefinition GetEvolutionForStyle(WeaponStyleId styleId)
        {
            return evolutions.FirstOrDefault(definition => definition.StyleId == styleId);
        }

        public static string GetChapterName(int stageNumber)
        {
            return Math.Clamp((Math.Max(1, stageNumber) - 1) / 10 + 1, 1, 5) switch
            {
                1 => "CHAPTER 1: OVERDRIVE",
                2 => "CHAPTER 2: FRACTURE",
                3 => "CHAPTER 3: CRUCIBLE",
                4 => "CHAPTER 4: PARALLAX",
                _ => "CHAPTER 5: EVENT HORIZON",
            };
        }

        private static WeaponEvolutionDefinition Evolution(
            EvolutionId id,
            WeaponStyleId styleId,
            PassiveReactorId requiredPassive,
            int requiredLevel,
            string title,
            string description,
            string previewText,
            string deltaText)
        {
            return new WeaponEvolutionDefinition
            {
                Id = id,
                StyleId = styleId,
                RequiredPassive = requiredPassive,
                RequiredLevel = requiredLevel,
                Title = title,
                Description = description,
                PreviewText = previewText,
                DeltaText = deltaText,
            };
        }
    }
}
