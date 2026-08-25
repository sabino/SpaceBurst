using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceBurst
{
    static class PersistentStorage
    {
        private static readonly JsonSerializerOptions jsonOptions = CreateOptions();
        private const string OptionsKey = "options.json";
        private const string MedalsKey = "medals.json";
        private const string HighScoreKey = "highscore.txt";
        private const string BackupSuffix = ".backup";
        private static string lastNotice = string.Empty;
        private static IStorageBackend Storage
        {
            get
            {
                PlatformServices.EnsureInitialized();
                return PlatformServices.Storage;
            }
        }

        private static string BaseDirectory
        {
            get
            {
                PlatformServices.EnsureInitialized();
                return Storage.GetDisplayPath(string.Empty);
            }
        }

        public static string UserDataDirectory
        {
            get { return BaseDirectory; }
        }

        public static string ConfigDirectory
        {
            get
            {
                return Storage.GetDisplayPath("config");
            }
        }

        public static string LastNotice
        {
            get { return lastNotice; }
        }

        private static string GetRunSlotKey(int slotIndex)
        {
            return string.Concat("slot-", Math.Clamp(slotIndex, 1, 3).ToString(), ".json");
        }

        public static string GetConfigFilePath(string fileName)
        {
            return Storage.GetDisplayPath(GetConfigKey(fileName));
        }

        public static bool ConfigFileExists(string fileName)
        {
            try
            {
                return Storage.Exists(GetConfigKey(fileName));
            }
            catch
            {
                return false;
            }
        }

        public static string ReadConfigText(string fileName)
        {
            try
            {
                string key = GetConfigKey(fileName);
                return Storage.Exists(key) ? Storage.ReadAllText(key) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string[] ReadConfigLines(string fileName)
        {
            try
            {
                string text = ReadConfigText(fileName);
                return text.Length == 0 ? Array.Empty<string>() : SplitLines(text);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public static void WriteConfigText(string fileName, string contents)
        {
            Storage.WriteAllText(GetConfigKey(fileName), contents ?? string.Empty);
        }

        public static void WriteConfigLines(string fileName, string[] lines)
        {
            WriteConfigText(fileName, string.Join(Environment.NewLine, lines ?? Array.Empty<string>()));
        }

        public static void AppendConfigLine(string fileName, string line)
        {
            string key = GetConfigKey(fileName);
            string existing = string.Empty;
            try
            {
                if (Storage.Exists(key))
                    existing = Storage.ReadAllText(key);
            }
            catch
            {
                existing = string.Empty;
            }

            string combined = existing.Length == 0
                ? string.Concat(line ?? string.Empty, Environment.NewLine)
                : string.Concat(existing, line ?? string.Empty, Environment.NewLine);
            Storage.WriteAllText(key, combined);
        }

        public static OptionsData LoadOptions()
        {
            try
            {
                if (!Storage.Exists(OptionsKey))
                    return new OptionsData();

                string json = Storage.ReadAllText(OptionsKey);
                OptionsData options = JsonSerializer.Deserialize<OptionsData>(json, jsonOptions) ?? new OptionsData();

                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                if (!root.TryGetProperty(nameof(OptionsData.UiScalePercent), out JsonElement uiScale))
                    options.UiScalePercent = UiScaleHelper.ClampUiScalePercent(options.UiScalePercent);
                else
                    options.UiScalePercent = UiScaleHelper.ClampUiScalePercent(uiScale.GetInt32());

                if (root.TryGetProperty(nameof(OptionsData.TouchControlsOpacity), out JsonElement touchOpacity))
                    options.TouchControlsOpacity = UiScaleHelper.ClampTouchControlsOpacity(touchOpacity.GetInt32());
                else
                    options.TouchControlsOpacity = UiScaleHelper.ClampTouchControlsOpacity(options.TouchControlsOpacity);

                if (!root.TryGetProperty(nameof(OptionsData.FontTheme), out _))
                {
#if ANDROID
                    options.FontTheme = FontTheme.Readable;
#else
                    options.FontTheme = FontTheme.Compact;
#endif
                }

                bool migratedHorizontalDefault = false;
                if (root.TryGetProperty(nameof(OptionsData.HasMigrated3DHorizontalDefault), out JsonElement horizontalMigration))
                    migratedHorizontalDefault = horizontalMigration.ValueKind == JsonValueKind.True;

                if (!migratedHorizontalDefault)
                {
                    options.Invert3DHorizontal = true;
                    options.HasMigrated3DHorizontalDefault = true;
                }

                bool migratedAudioMixDefault = root.TryGetProperty(nameof(OptionsData.HasMigratedAudioMixDefault), out JsonElement audioMixMigration)
                    && audioMixMigration.ValueKind == JsonValueKind.True;
                bool hasPersistedAudioMix = root.TryGetProperty(nameof(OptionsData.MusicVolume), out _)
                    && root.TryGetProperty(nameof(OptionsData.SfxVolume), out _);
                MigrateAudioMixDefaults(options, migratedAudioMixDefault, hasPersistedAudioMix);

                NormalizeAudioVolumes(options);

                return options;
            }
            catch
            {
                return new OptionsData();
            }
        }

        public static void SaveOptions(OptionsData options)
        {
            if (options == null)
                return;

            options.UiScalePercent = UiScaleHelper.ClampUiScalePercent(options.UiScalePercent);
            options.TouchControlsOpacity = UiScaleHelper.ClampTouchControlsOpacity(options.TouchControlsOpacity);
            options.HasMigratedAudioMixDefault = true;
            NormalizeAudioVolumes(options);
            SaveFile(OptionsKey, options);
        }

        internal static void MigrateAudioMixDefaults(OptionsData options, bool migrationRecorded, bool hasPersistedAudioMix)
        {
            if (options == null)
                return;

            if (!migrationRecorded
                && hasPersistedAudioMix
                && MathF.Abs(options.MusicVolume - OptionsData.LegacyDefaultMusicVolume) <= 0.001f
                && MathF.Abs(options.SfxVolume - OptionsData.LegacyDefaultSfxVolume) <= 0.001f)
            {
                options.MusicVolume = OptionsData.DefaultMusicVolume;
                options.SfxVolume = OptionsData.DefaultSfxVolume;
            }

            options.HasMigratedAudioMixDefault = true;
        }

        private static void NormalizeAudioVolumes(OptionsData options)
        {
            options.MasterVolume = Math.Clamp(options.MasterVolume, 0f, 1f);
            options.MusicVolume = Math.Clamp(options.MusicVolume, 0f, 1f);
            options.SfxVolume = Math.Clamp(options.SfxVolume, 0f, 1f);
        }

        public static MedalProgress LoadMedals()
        {
            return LoadFile(MedalsKey, new MedalProgress());
        }

        public static void SaveMedals(MedalProgress medals)
        {
            SaveFile(MedalsKey, medals);
        }

        public static RunSaveData LoadRunSlot(int slotIndex)
        {
            RunSaveData save = LoadRunSlotCore(slotIndex, true, out string notice);
            lastNotice = notice;
            return save;
        }

        private static RunSaveData LoadRunSlotCore(int slotIndex, bool reportEmptySlot, out string notice)
        {
            notice = string.Empty;
            string logicalKey = GetRunSlotKey(slotIndex);
            bool primaryExists = SafeExists(logicalKey);
            if (TryReadFile(logicalKey, out RunSaveData primary) && RunSaveIntegrity.TryPrepareForLoad(primary, jsonOptions, out string primaryWarning))
            {
                if (!string.IsNullOrWhiteSpace(primaryWarning))
                    notice = primaryWarning;
                return primary;
            }

            string backupKey = string.Concat(logicalKey, BackupSuffix);
            bool backupExists = SafeExists(backupKey);
            if (TryReadFile(backupKey, out RunSaveData backup) && RunSaveIntegrity.TryPrepareForLoad(backup, jsonOptions, out _))
            {
                notice = string.Concat("SLOT ", Math.Clamp(slotIndex, 1, 3).ToString(), " RECOVERED FROM BACKUP");
                return backup;
            }

            if (primaryExists || backupExists)
                notice = string.Concat("SLOT ", Math.Clamp(slotIndex, 1, 3).ToString(), " IS DAMAGED; START A NEW SAVE OR RESTORE A BACKUP");
            else if (reportEmptySlot)
                notice = string.Concat("SLOT ", Math.Clamp(slotIndex, 1, 3).ToString(), " IS EMPTY");
            return null;
        }

        public static void SaveRunSlot(int slotIndex, RunSaveData data)
        {
            if (data == null)
                return;

            try
            {
                RunSaveIntegrity.Seal(data, jsonOptions);
                SaveFile(
                    GetRunSlotKey(slotIndex),
                    data,
                    candidate => RunSaveIntegrity.TryPrepareForLoad(candidate, jsonOptions, out _));
            }
            catch
            {
                lastNotice = string.Concat("COULD NOT WRITE ", Path.GetFileName(GetRunSlotKey(slotIndex)).ToUpperInvariant(), "; CHECK SAVE DATA AND STORAGE SPACE");
            }
        }

        public static SaveSlotSummary[] LoadSaveSlotSummaries()
        {
            var summaries = new SaveSlotSummary[3];
            string summaryNotice = string.Empty;
            for (int slotIndex = 1; slotIndex <= 3; slotIndex++)
            {
                RunSaveData save = LoadRunSlotCore(slotIndex, false, out string notice);
                if (summaryNotice.Length == 0 && notice.Length > 0)
                    summaryNotice = notice;
                summaries[slotIndex - 1] = save?.Summary ?? new SaveSlotSummary
                {
                    SlotIndex = slotIndex,
                    HasData = false,
                };
            }

            lastNotice = summaryNotice;
            return summaries;
        }

        public static int LoadHighScore()
        {
            try
            {
                if (!Storage.Exists(HighScoreKey))
                    return 0;

                return int.TryParse(Storage.ReadAllText(HighScoreKey), out int score) ? score : 0;
            }
            catch
            {
                return 0;
            }
        }

        public static void SaveHighScore(int score)
        {
            Storage.WriteAllText(HighScoreKey, score.ToString());
        }

        private static T LoadFile<T>(string logicalKey, T fallback) where T : class
        {
            if (TryReadFile(logicalKey, out T primary))
                return primary;

            if (TryReadFile(string.Concat(logicalKey, BackupSuffix), out T backup))
            {
                lastNotice = string.Concat(Path.GetFileName(logicalKey).ToUpperInvariant(), " RECOVERED FROM BACKUP");
                return backup;
            }

            return fallback;
        }

        private static void SaveFile<T>(string logicalKey, T value, Func<T, bool> backupValidator = null) where T : class
        {
            try
            {
                if (TryReadFile(logicalKey, out T existing) && (backupValidator == null || backupValidator(existing)))
                    Storage.WriteAllText(string.Concat(logicalKey, BackupSuffix), JsonSerializer.Serialize(existing, jsonOptions));

                Storage.WriteAllText(logicalKey, JsonSerializer.Serialize(value, jsonOptions));
                if (logicalKey.StartsWith("slot-", StringComparison.OrdinalIgnoreCase))
                    lastNotice = string.Empty;
            }
            catch
            {
                lastNotice = string.Concat("COULD NOT WRITE ", Path.GetFileName(logicalKey).ToUpperInvariant(), "; CHECK STORAGE SPACE AND PERMISSIONS");
            }
        }

        private static bool TryReadFile<T>(string logicalKey, out T value) where T : class
        {
            value = null;
            try
            {
                if (!Storage.Exists(logicalKey))
                    return false;

                value = JsonSerializer.Deserialize<T>(Storage.ReadAllText(logicalKey), jsonOptions);
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool SafeExists(string logicalKey)
        {
            try
            {
                return Storage.Exists(logicalKey);
            }
            catch
            {
                return false;
            }
        }

        private static string GetConfigKey(string fileName)
        {
            return string.Concat("config/", SanitizeRelativeFileName(fileName));
        }

        private static string SanitizeRelativeFileName(string fileName)
        {
            string safe = string.IsNullOrWhiteSpace(fileName) ? "default.cfg" : fileName.Trim();
            safe = safe.Replace('\\', '/');
            while (safe.StartsWith("/", StringComparison.Ordinal))
                safe = safe.Substring(1);

            safe = string.Join("/", safe.Split('/', StringSplitOptions.RemoveEmptyEntries));
            return safe;
        }

        private static string[] SplitLines(string text)
        {
            var lines = new List<string>();
            using var reader = new StringReader(text ?? string.Empty);
            while (reader.ReadLine() is string line)
                lines.Add(line);

            return lines.ToArray();
        }

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions();
            options.WriteIndented = true;
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}
