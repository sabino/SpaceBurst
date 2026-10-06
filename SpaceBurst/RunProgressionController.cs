using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;

namespace SpaceBurst
{
    sealed class RunProgressionController
    {
        private static readonly PassiveReactorId[] SlicePassives =
        {
            PassiveReactorId.Overclock,
            PassiveReactorId.MagnetCore,
            PassiveReactorId.ArmorPlating,
            PassiveReactorId.TimeBattery,
            PassiveReactorId.SalvageNode,
            PassiveReactorId.ChainReactor,
        };

        public List<UpgradeDraftCard> BuildDraftCards(PlayerRunProgress progress, DeterministicRngState rng, bool tutorialMode, int stageNumber = 1, int currentShips = 0)
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

            var pool = new List<UpgradeDraftCard>();
            if (progress.Weapons.CanUpgradeStyle(progress.Weapons.ActiveStyle))
                pool.Add(CreateWeaponSurgeCard(progress.Weapons.ActiveStyle, progress, "CORE"));

            AddEvolutionCards(pool, progress);
            AddSupportWeaponCards(pool, progress, stageNumber);
            AddPassiveCards(pool, progress);
            AddSupportUpgradeCards(pool, progress);
            pool.Add(CreateRewindCard(progress));
            if (currentShips < PlayerStatus.MaximumShips)
                pool.Add(CreateScrapCacheCard(progress, progress.RunLevel >= 6 ? 3 : 2));

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
                    WeaponUpgradeOutcome outcome = progress.ApplyWeaponUpgrade(card.StyleId, card.StyleId == progress.Weapons.ActiveStyle);
                    if (outcome == WeaponUpgradeOutcome.NoChange)
                        return false;
                    if (card.StyleId != progress.Weapons.ActiveStyle)
                        progress.TryEquipSupportWeapon(card.StyleId);
                    return true;

                case UpgradeCardType.SupportWeapon:
                    if (progress.Weapons.HasSupportCapacity)
                        return progress.TryEquipSupportWeapon(card.StyleId);

                    if (progress.Weapons.OwnsStyle(card.StyleId))
                        return false;

                    return progress.ApplyWeaponUpgrade(card.StyleId, true) == WeaponUpgradeOutcome.UnlockedStyle;

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
                    PlayerStatus.GrantShips(progress.AddScrap(Math.Max(1, card.RewardAmount)));
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
                    PlayerStatus.GrantShips(progress.AddScrap(1));
                    return true;

                default:
                    return false;
            }
        }

        private static void AddEvolutionCards(List<UpgradeDraftCard> pool, PlayerRunProgress progress)
        {
            for (int index = 0; index < WeaponProgressionCatalog.Evolutions.Count; index++)
            {
                WeaponEvolutionDefinition definition = WeaponProgressionCatalog.Evolutions[index];
                if (!progress.Weapons.OwnsStyle(definition.StyleId) ||
                    progress.Weapons.GetLevel(definition.StyleId) < definition.RequiredLevel ||
                    !progress.HasPassive(definition.RequiredPassive) ||
                    progress.HasEvolution(definition.Id))
                {
                    continue;
                }

                pool.Add(new UpgradeDraftCard
                {
                    Type = UpgradeCardType.EvolutionSurge,
                    EvolutionId = definition.Id,
                    StyleId = definition.StyleId,
                    Title = definition.Title,
                    Subtitle = "EVOLUTION",
                    Description = definition.Description,
                    PreviewText = definition.PreviewText,
                    DeltaText = definition.DeltaText,
                    BadgeText = "EVOLVE",
                    AccentColor = WeaponCatalog.GetStyle(definition.StyleId).AccentColor,
                });
            }
        }

        private static void AddSupportWeaponCards(List<UpgradeDraftCard> pool, PlayerRunProgress progress, int stageNumber)
        {
            bool requiresCoreSwap = !progress.Weapons.HasSupportCapacity;
            IReadOnlyList<WeaponStyleId> availableStyles = WeaponProgressionCatalog.GetAvailableStyles(stageNumber);
            for (int i = 0; i < availableStyles.Count; i++)
            {
                WeaponStyleId style = availableStyles[i];
                if (style == progress.Weapons.ActiveStyle || progress.Weapons.HasSupportWeapon(style) || (requiresCoreSwap && progress.Weapons.OwnsStyle(style)))
                    continue;

                pool.Add(CreateSupportWeaponCard(style, requiresCoreSwap ? "CORE SWAP" : "STACK", requiresCoreSwap));
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
                if (progress.Weapons.CanUpgradeStyle(style))
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

        private static UpgradeDraftCard CreateSupportWeaponCard(WeaponStyleId styleId, string badge, bool replacesCore = false)
        {
            WeaponStyleDefinition style = WeaponCatalog.GetStyle(styleId);
            return new UpgradeDraftCard
            {
                Type = UpgradeCardType.SupportWeapon,
                StyleId = styleId,
                Title = string.Concat(style.DisplayName, replacesCore ? " CORE" : " WING"),
                Subtitle = replacesCore ? "ARSENAL UNLOCK" : "AUTO FIRE",
                Description = replacesCore
                    ? string.Concat("UNLOCK ", style.DisplayName, " AS CORE; ROTATE BACK WITH Q OR E")
                    : string.Concat("ADD ", style.DisplayName, " AS A SUPPORT WEAPON"),
                PreviewText = GetWeaponPreview(styleId),
                DeltaText = replacesCore ? "NEW CORE" : "STACK +1",
                BadgeText = badge,
                AccentColor = style.AccentColor,
            };
        }

        private static UpgradeDraftCard CreatePassiveCard(PassiveReactorId passive)
        {
            return passive switch
            {
                PassiveReactorId.Overclock => CreatePassiveCard(passive, "OVERCLOCK", "REACTOR", "BOOSTS FIRE RATE AND UNLOCKS PULSE OR RAIL EVOLUTIONS", "RATE +"),
                PassiveReactorId.MagnetCore => CreatePassiveCard(passive, "MAGNET CORE", "REACTOR", "PULLS SHARDS HARDER AND UNLOCKS SPREAD EVOLUTION", "MAGNET +"),
                PassiveReactorId.ArmorPlating => CreatePassiveCard(passive, "ARMOR PLATING", "REACTOR", "ADDS SHIPS AND UNLOCKS BLADE OR FORTRESS EVOLUTIONS", "SHIPS +1"),
                PassiveReactorId.TimeBattery => CreatePassiveCard(passive, "TIME BATTERY", "REACTOR", "BUFFS REWIND AND UNLOCKS PLASMA OR DRONE EVOLUTIONS", "REWIND +"),
                PassiveReactorId.SalvageNode => CreatePassiveCard(passive, "SALVAGE NODE", "REACTOR", "BOOSTS SCRAP FLOW AND UNLOCKS MISSILE EVOLUTION", "SCRAP +"),
                _ => CreatePassiveCard(passive, "CHAIN REACTOR", "REACTOR", "ADDS SEEKING AND UNLOCKS LASER OR ARC EVOLUTIONS", "CHAIN +"),
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
                DeltaText = string.Concat("DRAIN -", MathF.Round(MathF.Min(0.6f, progress.RewindEfficiency + 0.12f) * 100f).ToString("0"), "%"),
                BadgeText = "TIME",
                AccentColor = "#56F0FF",
            };
        }

        private static UpgradeDraftCard CreateScrapCacheCard(PlayerRunProgress progress, int amount)
        {
            return new UpgradeDraftCard
            {
                Type = UpgradeCardType.ScrapCache,
                Title = "SALVAGE CACHE",
                Subtitle = "SPARE SHIP",
                Description = "EVERY 5 SCRAP BUILDS A SPARE SHIP FOR AN IN PLACE RESPAWN",
                PreviewText = string.Concat("NEXT SHIP IN ", (PlayerRunProgress.ScrapPerShip - progress.ScrapTowardNextShip).ToString()),
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
                WeaponStyleId.Spread => "NOVA FAN",
                WeaponStyleId.Laser => "PRISM BEAM",
                WeaponStyleId.Plasma => "CHRONO BLAST",
                WeaponStyleId.Missile => "HOMING BLAST",
                WeaponStyleId.Rail => "VOID PIERCE",
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
