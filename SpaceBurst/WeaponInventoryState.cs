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

        public WeaponStyleId ActiveStyle { get; private set; } = WeaponStyleId.Pulse;
        public int StoredUpgradeCharges { get; private set; }

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

        public void Reset()
        {
            styleLevels.Clear();
            styleRanks.Clear();
            styleLevels[WeaponStyleId.Pulse] = 0;
            ActiveStyle = WeaponStyleId.Pulse;
            StoredUpgradeCharges = 0;
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

        public void AddUpgradeCharge(int count = 1)
        {
            StoredUpgradeCharges = Math.Max(0, StoredUpgradeCharges + count);
        }

        public bool ConsumeUpgradeCharge()
        {
            if (StoredUpgradeCharges <= 0)
                return false;

            StoredUpgradeCharges--;
            return true;
        }

        public WeaponUpgradeOutcome ApplyWeaponUpgrade()
        {
            int level = ActiveLevel;
            if (level < 3)
            {
                styleLevels[ActiveStyle] = level + 1;
                return WeaponUpgradeOutcome.LevelUp;
            }

            for (int i = 0; i < WeaponCatalog.StyleOrder.Count; i++)
            {
                WeaponStyleId nextStyle = WeaponCatalog.StyleOrder[i];
                if (styleLevels.ContainsKey(nextStyle))
                    continue;

                styleLevels[nextStyle] = 0;
                styleRanks[nextStyle] = 0;
                ActiveStyle = nextStyle;
                return WeaponUpgradeOutcome.UnlockedStyle;
            }

            styleRanks[ActiveStyle] = GetRank(ActiveStyle) + 1;
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

            if (!styleLevels.ContainsKey(WeaponStyleId.Pulse))
                styleLevels[WeaponStyleId.Pulse] = 0;
            if (!styleRanks.ContainsKey(WeaponStyleId.Pulse))
                styleRanks[WeaponStyleId.Pulse] = 0;

            ActiveStyle = snapshot != null && styleLevels.ContainsKey(snapshot.ActiveStyle)
                ? snapshot.ActiveStyle
                : WeaponStyleId.Pulse;
            StoredUpgradeCharges = Math.Max(0, snapshot?.StoredUpgradeCharges ?? 0);
        }
    }
}
