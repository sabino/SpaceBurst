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
            SaveFile(OptionsKey, options);
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
            return LoadFile<RunSaveData>(GetRunSlotKey(slotIndex), null);
        }

        public static void SaveRunSlot(int slotIndex, RunSaveData data)
        {
            if (data == null)
                return;

            SaveFile(GetRunSlotKey(slotIndex), data);
        }

        public static SaveSlotSummary[] LoadSaveSlotSummaries()
        {
            var summaries = new SaveSlotSummary[3];
            for (int slotIndex = 1; slotIndex <= 3; slotIndex++)
            {
                RunSaveData save = LoadRunSlot(slotIndex);
                summaries[slotIndex - 1] = save?.Summary ?? new SaveSlotSummary
                {
                    SlotIndex = slotIndex,
                    HasData = false,
                };
            }

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
            try
            {
                if (!Storage.Exists(logicalKey))
                    return fallback;

                string json = Storage.ReadAllText(logicalKey);
                return JsonSerializer.Deserialize<T>(json, jsonOptions) ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static void SaveFile<T>(string logicalKey, T value)
        {
            Storage.WriteAllText(logicalKey, JsonSerializer.Serialize(value, jsonOptions));
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
