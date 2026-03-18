using SpaceBurst.RuntimeData;
using System.Collections.Generic;
using System.Linq;

namespace SpaceBurst
{
    enum PowerupCollectOutcome
    {
        LevelUp,
        UnlockedStyle,
        OverflowReward,
    }

    sealed class WeaponInventoryState
    {
        private readonly Dictionary<WeaponStyleId, int> styleLevels = new Dictionary<WeaponStyleId, int>();

        public WeaponStyleId ActiveStyle { get; private set; } = WeaponStyleId.Pulse;

        public int ActiveLevel
        {
            get { return GetLevel(ActiveStyle); }
        }

        public IReadOnlyList<WeaponStyleId> OwnedStyles
        {
            get { return WeaponCatalog.StyleOrder.Where(styleLevels.ContainsKey).ToList(); }
        }

        public void Reset()
        {
            styleLevels.Clear();
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

        public PowerupCollectOutcome ApplyPowerup()
        {
            int level = ActiveLevel;
            if (level < 3)
            {
                styleLevels[ActiveStyle] = level + 1;
                return PowerupCollectOutcome.LevelUp;
            }

            for (int i = 0; i < WeaponCatalog.StyleOrder.Count; i++)
            {
                WeaponStyleId nextStyle = WeaponCatalog.StyleOrder[i];
                if (styleLevels.ContainsKey(nextStyle))
                    continue;

                styleLevels[nextStyle] = 0;
                ActiveStyle = nextStyle;
                return PowerupCollectOutcome.UnlockedStyle;
            }

            return PowerupCollectOutcome.OverflowReward;
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
                styleLevels.Remove(removable);
        }

        private void RemoveStyle(WeaponStyleId style)
        {
            styleLevels.Remove(style);
            IReadOnlyList<WeaponStyleId> styles = OwnedStyles;
            ActiveStyle = styles.Count == 0 ? WeaponStyleId.Pulse : styles[styles.Count - 1];
            if (!styleLevels.ContainsKey(WeaponStyleId.Pulse))
                styleLevels[WeaponStyleId.Pulse] = 0;
        }
    }
}
