using Xunit;

namespace SpaceBurst.Tests
{
    public sealed class AudioMixDefaultsTests
    {
        [Fact]
        public void NewOptions_FavorMusicOverSoundEffects()
        {
            var options = new OptionsData();

            Assert.Equal(0.9f, options.MusicVolume);
            Assert.Equal(0.7f, options.SfxVolume);
            Assert.True(options.MusicVolume > options.SfxVolume);
        }

        [Fact]
        public void DefaultPickupAndUpgradeCues_KeepMixHeadroom()
        {
            float highlightedPickupGain = OptionsData.DefaultSfxVolume * AudioDirector.HighlightPickupCueVolume;
            float upgradeGain = OptionsData.DefaultSfxVolume * AudioDirector.UpgradeCueVolume;

            Assert.InRange(highlightedPickupGain, 0f, 0.12f);
            Assert.InRange(upgradeGain, 0f, 0.13f);
        }

        [Fact]
        public void LegacyUntouchedDefaults_MigrateToBalancedMix()
        {
            var options = new OptionsData
            {
                MusicVolume = OptionsData.LegacyDefaultMusicVolume,
                SfxVolume = OptionsData.LegacyDefaultSfxVolume,
                HasMigratedAudioMixDefault = false,
            };

            PersistentStorage.MigrateAudioMixDefaults(options, migrationRecorded: false, hasPersistedAudioMix: true);

            Assert.Equal(OptionsData.DefaultMusicVolume, options.MusicVolume);
            Assert.Equal(OptionsData.DefaultSfxVolume, options.SfxVolume);
            Assert.True(options.HasMigratedAudioMixDefault);
        }

        [Fact]
        public void LegacyCustomMix_IsNotOverwritten()
        {
            var options = new OptionsData
            {
                MusicVolume = 0.55f,
                SfxVolume = 0.4f,
                HasMigratedAudioMixDefault = false,
            };

            PersistentStorage.MigrateAudioMixDefaults(options, migrationRecorded: false, hasPersistedAudioMix: true);

            Assert.Equal(0.55f, options.MusicVolume);
            Assert.Equal(0.4f, options.SfxVolume);
            Assert.True(options.HasMigratedAudioMixDefault);
        }
    }
}
