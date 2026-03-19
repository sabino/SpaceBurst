using System.Collections.Generic;

namespace SpaceBurst
{
    sealed class MedalProgress
    {
        public HashSet<int> StageClearLevels { get; set; } = new HashSet<int>();
        public HashSet<int> NoDeathLevels { get; set; } = new HashSet<int>();
        public HashSet<int> BossClearLevels { get; set; } = new HashSet<int>();
        public bool CampaignClear { get; set; }
        public bool PerfectCampaign { get; set; }

        public void UnlockStageClear(int levelNumber)
        {
            StageClearLevels.Add(levelNumber);
        }

        public void UnlockNoDeath(int levelNumber)
        {
            NoDeathLevels.Add(levelNumber);
        }

        public void UnlockBossClear(int levelNumber)
        {
            BossClearLevels.Add(levelNumber);
        }
    }
}
