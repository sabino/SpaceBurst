using SpaceBurst.RuntimeData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceBurst
{
    enum WeaponUpgradeOutcome
    {
        LevelUp,
        UnlockedStyle,
        RankUp,
        NoChange,
    }

    sealed class WeaponInventoryState
    {
        private readonly Dictionary<WeaponStyleId, int> styleLevels = new Dictionary<WeaponStyleId, int>();
        private readonly Dictionary<WeaponStyleId, int> styleRanks = new Dictionary<WeaponStyleId, int>();
        private readonly Dictionary<WeaponStyleId, int> styleCharges = new Dictionary<WeaponStyleId, int>();

        public WeaponStyleId ActiveStyle { get; private set; } = WeaponStyleId.Pulse;
        public int StoredUpgradeCharges
        {
            get { return styleCharges.Values.Sum(); }
        }

        public int ActiveLevel
        {
            get { return GetLevel(ActiveStyle); }
        }

        public int ActiveRank
        {
            get { return GetRank(ActiveStyle); }
        }

        public int HighestRank
        {
            get { return styleRanks.Count == 0 ? 0 : styleRanks.Values.Max(); }
        }

        public IReadOnlyList<WeaponStyleId> OwnedStyles
        {
            get { return WeaponCatalog.StyleOrder.Where(styleLevels.ContainsKey).ToList(); }
        }

        public int UnlockedStyleCount
        {
            get { return styleLevels.Count; }
        }

        public IReadOnlyList<WeaponStyleId> ChargedStyles
        {
            get { return WeaponCatalog.StyleOrder.Where(style => GetStoredCharge(style) > 0).ToList(); }
        }

        public void Reset()
        {
            styleLevels.Clear();
            styleRanks.Clear();
            styleCharges.Clear();
            styleLevels[WeaponStyleId.Pulse] = 0;
            ActiveStyle = WeaponStyleId.Pulse;
        }

        public bool OwnsStyle(WeaponStyleId style)
        {
            return styleLevels.ContainsKey(style);
        }

        public int GetLevel(WeaponStyleId style)
        {
            return styleLevels.TryGetValue(style, out int level) ? level : -1;
        }

        public int GetRank(WeaponStyleId style)
        {
            return styleRanks.TryGetValue(style, out int rank) ? rank : 0;
        }

        public int GetStoredCharge(WeaponStyleId style)
        {
            return styleCharges.TryGetValue(style, out int count) ? count : 0;
        }

        public WeaponStyleId GetPriorityChargeStyle()
        {
            if (GetStoredCharge(ActiveStyle) > 0)
                return ActiveStyle;

            WeaponStyleId nextLocked = WeaponCatalog.StyleOrder.FirstOrDefault(style => GetStoredCharge(style) > 0 && !OwnsStyle(style));
            if (nextLocked != 0)
                return nextLocked;

            WeaponStyleId best = WeaponStyleId.Pulse;
            int bestCount = 0;
            foreach (WeaponStyleId style in WeaponCatalog.StyleOrder)
            {
                int count = GetStoredCharge(style);
                if (count > bestCount)
                {
                    bestCount = count;
                    best = style;
                }
            }

            return bestCount > 0 ? best : WeaponStyleId.Pulse;
        }

        public void AddUpgradeCharge(WeaponStyleId style, int count = 1)
        {
            if (count <= 0)
                return;

            styleCharges[style] = GetStoredCharge(style) + count;
        }

        public bool ConsumeUpgradeCharge(WeaponStyleId style)
        {
            int current = GetStoredCharge(style);
            if (current <= 0)
                return false;

            if (current == 1)
                styleCharges.Remove(style);
            else
                styleCharges[style] = current - 1;
            return true;
        }

        public WeaponUpgradeOutcome ApplyWeaponUpgrade()
        {
            return ApplyWeaponUpgrade(ActiveStyle, false);
        }

        public WeaponUpgradeOutcome ApplyWeaponUpgrade(WeaponStyleId style, bool activateStyle)
        {
            if (!OwnsStyle(style))
            {
                styleLevels[style] = 0;
                styleRanks[style] = 0;
                if (activateStyle)
                    ActiveStyle = style;
                return WeaponUpgradeOutcome.UnlockedStyle;
            }

            int level = GetLevel(style);
            if (level < 3)
            {
                styleLevels[style] = level + 1;
                if (activateStyle)
                    ActiveStyle = style;
                return WeaponUpgradeOutcome.LevelUp;
            }

            styleRanks[style] = GetRank(style) + 1;
            if (activateStyle)
                ActiveStyle = style;
            return WeaponUpgradeOutcome.RankUp;
        }

        public void Cycle(int direction)
        {
            List<WeaponStyleId> styles = OwnedStyles.ToList();
            if (styles.Count <= 1)
                return;

            int currentIndex = styles.IndexOf(ActiveStyle);
            if (currentIndex < 0)
                currentIndex = 0;

            currentIndex = (currentIndex + styles.Count + direction) % styles.Count;
            ActiveStyle = styles[currentIndex];
        }

        public void ApplyDeathPenalty()
        {
            int currentLevel = ActiveLevel;
            if (currentLevel > 0)
            {
                styleLevels[ActiveStyle] = currentLevel - 1;
                return;
            }

            if (ActiveStyle != WeaponStyleId.Pulse)
            {
                RemoveStyle(ActiveStyle);
                return;
            }

            WeaponStyleId removable = WeaponCatalog.StyleOrder.LastOrDefault(style => style != WeaponStyleId.Pulse && styleLevels.ContainsKey(style));
            if (removable != WeaponStyleId.Pulse && styleLevels.ContainsKey(removable))
                RemoveStyle(removable);
        }

        public void SetStyleProgress(WeaponStyleId style, int level, int rank = 0, bool activate = false)
        {
            styleLevels[style] = Math.Clamp(level, 0, 3);
            styleRanks[style] = Math.Max(0, rank);
            if (activate)
                ActiveStyle = style;
        }

        public void SetActiveStyle(WeaponStyleId style)
        {
            if (OwnsStyle(style))
                ActiveStyle = style;
        }

        private void RemoveStyle(WeaponStyleId style)
        {
            styleLevels.Remove(style);
            styleRanks.Remove(style);
            styleCharges.Remove(style);
            IReadOnlyList<WeaponStyleId> styles = OwnedStyles;
            ActiveStyle = styles.Count == 0 ? WeaponStyleId.Pulse : styles[styles.Count - 1];
            if (!styleLevels.ContainsKey(WeaponStyleId.Pulse))
                styleLevels[WeaponStyleId.Pulse] = 0;
            if (!styleRanks.ContainsKey(WeaponStyleId.Pulse))
                styleRanks[WeaponStyleId.Pulse] = 0;
        }

        public WeaponInventorySnapshotData CaptureSnapshot()
        {
            return new WeaponInventorySnapshotData
            {
                ActiveStyle = ActiveStyle,
                StyleLevels = new Dictionary<WeaponStyleId, int>(styleLevels),
                StyleRanks = new Dictionary<WeaponStyleId, int>(styleRanks),
                StyleCharges = new Dictionary<WeaponStyleId, int>(styleCharges),
                StoredUpgradeCharges = StoredUpgradeCharges,
            };
        }

        public void RestoreSnapshot(WeaponInventorySnapshotData snapshot)
        {
            styleLevels.Clear();
            styleRanks.Clear();
            if (snapshot?.StyleLevels != null)
            {
                foreach (var entry in snapshot.StyleLevels)
                    styleLevels[entry.Key] = entry.Value;
            }

            if (snapshot?.StyleRanks != null)
            {
                foreach (var entry in snapshot.StyleRanks)
                    styleRanks[entry.Key] = Math.Max(0, entry.Value);
            }

            styleCharges.Clear();
            if (snapshot?.StyleCharges != null)
            {
                foreach (var entry in snapshot.StyleCharges)
                {
                    if (entry.Value > 0)
                        styleCharges[entry.Key] = entry.Value;
                }
            }

            if (!styleLevels.ContainsKey(WeaponStyleId.Pulse))
                styleLevels[WeaponStyleId.Pulse] = 0;
            if (!styleRanks.ContainsKey(WeaponStyleId.Pulse))
                styleRanks[WeaponStyleId.Pulse] = 0;

            ActiveStyle = snapshot != null && styleLevels.ContainsKey(snapshot.ActiveStyle)
                ? snapshot.ActiveStyle
                : WeaponStyleId.Pulse;
        }
    }
}
