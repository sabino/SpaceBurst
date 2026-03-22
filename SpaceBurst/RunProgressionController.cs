using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    sealed class RunProgressionController
    {
        private static readonly WeaponStyleId[] SliceSupportWeapons =
        {
            WeaponStyleId.Missile,
            WeaponStyleId.Arc,
            WeaponStyleId.Drone,
            WeaponStyleId.Blade,
            WeaponStyleId.Fortress,
        };

        private static readonly PassiveReactorId[] SlicePassives =
        {
            PassiveReactorId.Overclock,
            PassiveReactorId.MagnetCore,
            PassiveReactorId.ArmorPlating,
            PassiveReactorId.TimeBattery,
            PassiveReactorId.SalvageNode,
            PassiveReactorId.ChainReactor,
        };

        public List<UpgradeDraftCard> BuildDraftCards(PlayerRunProgress progress, DeterministicRngState rng, bool tutorialMode)
        {
            var cards = new List<UpgradeDraftCard>();
            if (progress == null)
                return cards;

            if (tutorialMode)
            {
                cards.Add(CreateSupportWeaponCard(WeaponStyleId.Spread, "TRAINING"));
                cards.Add(CreatePassiveCard(PassiveReactorId.MagnetCore));
                cards.Add(CreateRewindCard(progress));
                return cards;
            }

            List<UpgradeDraftCard> pool = new List<UpgradeDraftCard>
            {
                CreateWeaponSurgeCard(progress.Weapons.ActiveStyle, progress, "CORE"),
            };

            AddEvolutionCards(pool, progress);
            AddSupportWeaponCards(pool, progress);
            AddPassiveCards(pool, progress);
            AddSupportUpgradeCards(pool, progress);
            pool.Add(CreateRewindCard(progress));
            pool.Add(CreateScrapCacheCard(progress.RunLevel >= 6 ? 3 : 2));

            while (cards.Count < 3 && pool.Count > 0)
            {
                int index = rng != null ? rng.NextInt(0, pool.Count) : cards.Count % pool.Count;
                cards.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return cards;
        }

        public bool ApplyDraftSelection(PlayerRunProgress progress, UpgradeDraftCard card, ref float rewindMeterSeconds, float rewindCapacitySeconds)
        {
            if (progress == null || card == null)
                return false;

            switch (card.Type)
            {
                case UpgradeCardType.WeaponSurge:
                    progress.ApplyWeaponUpgrade(card.StyleId, card.StyleId == progress.Weapons.ActiveStyle);
                    if (card.StyleId != progress.Weapons.ActiveStyle)
                        progress.TryEquipSupportWeapon(card.StyleId);
                    return true;

                case UpgradeCardType.SupportWeapon:
                    progress.ApplyWeaponUpgrade(card.StyleId, false);
                    return progress.TryEquipSupportWeapon(card.StyleId);

                case UpgradeCardType.PassiveReactor:
                    if (!progress.TryEquipPassive(card.PassiveReactorId))
                        return false;

                    if (card.PassiveReactorId == PassiveReactorId.ArmorPlating)
                        PlayerStatus.GrantShips(1);
                    if (card.PassiveReactorId == PassiveReactorId.TimeBattery)
                        rewindMeterSeconds = rewindCapacitySeconds;
                    return true;

                case UpgradeCardType.EvolutionSurge:
                    if (!progress.TryAddEvolution(card.EvolutionId))
                        return false;

                    rewindMeterSeconds = Math.Min(rewindCapacitySeconds, rewindMeterSeconds + rewindCapacitySeconds * 0.18f);
                    return true;

                case UpgradeCardType.RewindBattery:
                    progress.ApplyRewindUpgrade();
                    rewindMeterSeconds = rewindCapacitySeconds;
                    return true;

                case UpgradeCardType.ScrapCache:
                    progress.AddScrap(Math.Max(1, card.RewardAmount));
                    return true;

                case UpgradeCardType.MobilityTuning:
                    progress.ApplyMobilityUpgrade();
                    return true;

                case UpgradeCardType.EmergencyReserve:
                    progress.ApplyEmergencyReserveUpgrade();
                    PlayerStatus.GrantShips(1);
                    return true;

                case UpgradeCardType.LuckyCore:
                    progress.ApplyEconomyUpgrade();
                    progress.AddScrap(1);
                    return true;

                default:
                    return false;
            }
        }

        private static void AddEvolutionCards(List<UpgradeDraftCard> pool, PlayerRunProgress progress)
        {
            if (progress.Weapons.ActiveStyle == WeaponStyleId.Pulse &&
                progress.Weapons.GetLevel(WeaponStyleId.Pulse) >= 3 &&
                progress.HasPassive(PassiveReactorId.Overclock) &&
                !progress.HasEvolution(EvolutionId.SingularityRail))
            {
                pool.Add(new UpgradeDraftCard
                {
                    Type = UpgradeCardType.EvolutionSurge,
                    EvolutionId = EvolutionId.SingularityRail,
                    StyleId = WeaponStyleId.Pulse,
                    Title = "SINGULARITY RAIL",
                    Subtitle = "EVOLUTION",
                    Description = "CORE PULSE COLLAPSES INTO A PIERCING HYPER-RAIL",
                    PreviewText = "PULSE -> RAIL",
                    DeltaText = "PIERCE + DMG",
                    BadgeText = "EVOLVE",
                    AccentColor = WeaponCatalog.GetStyle(WeaponStyleId.Rail).AccentColor,
                });
            }

            if (progress.Weapons.OwnsStyle(WeaponStyleId.Missile) &&
                progress.Weapons.GetLevel(WeaponStyleId.Missile) >= 2 &&
                progress.HasPassive(PassiveReactorId.SalvageNode) &&
                !progress.HasEvolution(EvolutionId.CataclysmRack))
            {
                pool.Add(new UpgradeDraftCard
                {
                    Type = UpgradeCardType.EvolutionSurge,
                    EvolutionId = EvolutionId.CataclysmRack,
                    StyleId = WeaponStyleId.Missile,
                    Title = "CATACLYSM RACK",
                    Subtitle = "EVOLUTION",
                    Description = "MISSILES SPLIT HARDER AND DETONATE WIDER",
                    PreviewText = "MISSILE ++",
                    DeltaText = "BLAST + VOLLEY",
                    BadgeText = "EVOLVE",
                    AccentColor = WeaponCatalog.GetStyle(WeaponStyleId.Missile).AccentColor,
                });
            }

            if (progress.Weapons.OwnsStyle(WeaponStyleId.Drone) &&
                progress.Weapons.GetLevel(WeaponStyleId.Drone) >= 2 &&
                progress.HasPassive(PassiveReactorId.TimeBattery) &&
                !progress.HasEvolution(EvolutionId.EchoHive))
            {
                pool.Add(new UpgradeDraftCard
                {
                    Type = UpgradeCardType.EvolutionSurge,
                    EvolutionId = EvolutionId.EchoHive,
                    StyleId = WeaponStyleId.Drone,
                    Title = "ECHO HIVE",
                    Subtitle = "EVOLUTION",
                    Description = "DRONES MULTIPLY AND FIRE THROUGH REWIND AFTERGLOWS",
                    PreviewText = "DRONE ++",
                    DeltaText = "DRONES + CHAIN",
                    BadgeText = "EVOLVE",
                    AccentColor = WeaponCatalog.GetStyle(WeaponStyleId.Drone).AccentColor,
                });
            }
        }

        private static void AddSupportWeaponCards(List<UpgradeDraftCard> pool, PlayerRunProgress progress)
        {
            for (int i = 0; i < SliceSupportWeapons.Length; i++)
            {
                WeaponStyleId style = SliceSupportWeapons[i];
                if (style == progress.Weapons.ActiveStyle || progress.Weapons.HasSupportWeapon(style))
                    continue;

                pool.Add(CreateSupportWeaponCard(style, "STACK"));
            }
        }

        private static void AddPassiveCards(List<UpgradeDraftCard> pool, PlayerRunProgress progress)
        {
            if (progress.Weapons.PassiveReactors.Count >= progress.PassiveSlots)
                return;

            for (int i = 0; i < SlicePassives.Length; i++)
            {
                PassiveReactorId passive = SlicePassives[i];
                if (progress.HasPassive(passive))
                    continue;

                pool.Add(CreatePassiveCard(passive));
            }
        }

        private static void AddSupportUpgradeCards(List<UpgradeDraftCard> pool, PlayerRunProgress progress)
        {
            for (int i = 0; i < progress.Weapons.SupportWeapons.Count; i++)
            {
                WeaponStyleId style = progress.Weapons.SupportWeapons[i];
                pool.Add(CreateWeaponSurgeCard(style, progress, "SUPPORT"));
            }
        }

        private static UpgradeDraftCard CreateWeaponSurgeCard(WeaponStyleId styleId, PlayerRunProgress progress, string badge)
        {
            WeaponInventoryState inventory = progress.Weapons;
            WeaponStyleDefinition style = WeaponCatalog.GetStyle(styleId);
            string subtitle;
            string description;
            string deltaText;

            if (!inventory.OwnsStyle(styleId))
            {
                subtitle = "UNLOCK";
                description = string.Concat("UNLOCK ", style.DisplayName, " FOR THE STACK");
                deltaText = "LV 0 -> LV 1";
            }
            else if (inventory.GetLevel(styleId) < 3)
            {
                subtitle = "LEVEL SURGE";
                description = string.Concat("BOOST ", style.DisplayName, " TO LEVEL ", (inventory.GetLevel(styleId) + 1).ToString());
                deltaText = string.Concat("LV ", inventory.GetLevel(styleId).ToString(), " -> LV ", (inventory.GetLevel(styleId) + 1).ToString());
            }
            else
            {
                subtitle = "RANK SURGE";
                description = string.Concat("OVERDRIVE ", style.DisplayName, " TO RANK ", (inventory.GetRank(styleId) + 1).ToString());
                deltaText = string.Concat("RK ", inventory.GetRank(styleId).ToString(), " -> RK ", (inventory.GetRank(styleId) + 1).ToString());
            }

            return new UpgradeDraftCard
            {
                Type = UpgradeCardType.WeaponSurge,
                StyleId = styleId,
                Title = string.Concat(style.DisplayName, " SURGE"),
                Subtitle = subtitle,
                Description = description,
                PreviewText = GetWeaponPreview(styleId),
                DeltaText = deltaText,
                BadgeText = badge,
                AccentColor = style.AccentColor,
            };
        }

        private static UpgradeDraftCard CreateSupportWeaponCard(WeaponStyleId styleId, string badge)
        {
            WeaponStyleDefinition style = WeaponCatalog.GetStyle(styleId);
            return new UpgradeDraftCard
            {
                Type = UpgradeCardType.SupportWeapon,
                StyleId = styleId,
                Title = string.Concat(style.DisplayName, " WING"),
                Subtitle = "AUTO FIRE",
                Description = string.Concat("ADD ", style.DisplayName, " AS A SUPPORT WEAPON"),
                PreviewText = GetWeaponPreview(styleId),
                DeltaText = "STACK +1",
                BadgeText = badge,
                AccentColor = style.AccentColor,
            };
        }

        private static UpgradeDraftCard CreatePassiveCard(PassiveReactorId passive)
        {
            return passive switch
            {
                PassiveReactorId.Overclock => CreatePassiveCard(passive, "OVERCLOCK", "REACTOR", "BOOSTS FIRE RATE AND UNLOCKS PULSE EVOLUTION", "RATE +"),
                PassiveReactorId.MagnetCore => CreatePassiveCard(passive, "MAGNET CORE", "REACTOR", "PULLS SHARDS HARDER AND IMPROVES CORE FLOW", "MAGNET +"),
                PassiveReactorId.ArmorPlating => CreatePassiveCard(passive, "ARMOR PLATING", "REACTOR", "ADDS SHIPS AND HOLDS THE LINE LONGER", "SHIPS +1"),
                PassiveReactorId.TimeBattery => CreatePassiveCard(passive, "TIME BATTERY", "REACTOR", "BUFFS REWIND AND UNLOCKS DRONE EVOLUTION", "REWIND +"),
                PassiveReactorId.SalvageNode => CreatePassiveCard(passive, "SALVAGE NODE", "REACTOR", "BOOSTS SCRAP FLOW AND UNLOCKS MISSILE EVOLUTION", "SCRAP +"),
                _ => CreatePassiveCard(passive, "CHAIN REACTOR", "REACTOR", "ADDS SEEKING AND ARC PRESSURE TO THE STACK", "CHAIN +"),
            };
        }

        private static UpgradeDraftCard CreatePassiveCard(PassiveReactorId passive, string title, string subtitle, string description, string deltaText)
        {
            return new UpgradeDraftCard
            {
                Type = UpgradeCardType.PassiveReactor,
                PassiveReactorId = passive,
                Title = title,
                Subtitle = subtitle,
                Description = description,
                PreviewText = "PASSIVE",
                DeltaText = deltaText,
                BadgeText = "REACTOR",
                AccentColor = ResolvePassiveAccent(passive),
            };
        }

        private static UpgradeDraftCard CreateRewindCard(PlayerRunProgress progress)
        {
            return new UpgradeDraftCard
            {
                Type = UpgradeCardType.RewindBattery,
                Title = "TIME BATTERY",
                Subtitle = "UTILITY",
                Description = "REFILL REWIND AND LOWER METER DRAIN",
                PreviewText = "REWIND",
                DeltaText = string.Concat("DRAIN -", MathF.Round((progress.RewindEfficiency + 0.12f) * 100f).ToString("0"), "%"),
                BadgeText = "TIME",
                AccentColor = "#56F0FF",
            };
        }

        private static UpgradeDraftCard CreateScrapCacheCard(int amount)
        {
            return new UpgradeDraftCard
            {
                Type = UpgradeCardType.ScrapCache,
                Title = "SALVAGE CACHE",
                Subtitle = "REWARD",
                Description = "BANK EXTRA SCRAP FOR FUTURE SHIP FRAMES",
                PreviewText = "SCRAP",
                DeltaText = string.Concat("+", amount.ToString(), " SCRAP"),
                BadgeText = "CACHE",
                AccentColor = "#FFB347",
                RewardAmount = amount,
            };
        }

        private static string GetWeaponPreview(WeaponStyleId styleId)
        {
            return styleId switch
            {
                WeaponStyleId.Pulse => "FOCUS RAIL",
                WeaponStyleId.Missile => "HOMING BLAST",
                WeaponStyleId.Arc => "CHAIN VOLT",
                WeaponStyleId.Drone => "HIVE FIRE",
                WeaponStyleId.Blade => "SCREEN COVER",
                WeaponStyleId.Fortress => "WALL BURST",
                _ => "POWER UP",
            };
        }

        private static string ResolvePassiveAccent(PassiveReactorId passive)
        {
            return passive switch
            {
                PassiveReactorId.Overclock => "#FF8BD7",
                PassiveReactorId.MagnetCore => "#73F3E8",
                PassiveReactorId.ArmorPlating => "#E9E3D2",
                PassiveReactorId.TimeBattery => "#56F0FF",
                PassiveReactorId.SalvageNode => "#FFB347",
                _ => "#7AE582",
            };
        }
    }
}
