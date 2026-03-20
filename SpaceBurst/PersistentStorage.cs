using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceBurst
{
    static class PersistentStorage
    {
        private static readonly JsonSerializerOptions jsonOptions = CreateOptions();
        private static readonly string documentsBaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SpaceBurst");
        private static readonly string legacyBaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpaceBurst");
        private static bool initialized;

        private static string BaseDirectory
        {
            get
            {
                EnsureInitialized();
                return documentsBaseDirectory;
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
                string path = Path.Combine(BaseDirectory, "config");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        private static string OptionsPath
        {
            get { return Path.Combine(BaseDirectory, "options.json"); }
        }

        private static string MedalsPath
        {
            get { return Path.Combine(BaseDirectory, "medals.json"); }
        }

        private static string GetRunSlotPath(int slotIndex)
        {
            return Path.Combine(BaseDirectory, string.Concat("slot-", Math.Clamp(slotIndex, 1, 3).ToString(), ".json"));
        }

        public static string GetConfigFilePath(string fileName)
        {
            return Path.Combine(ConfigDirectory, SanitizeRelativeFileName(fileName));
        }

        public static string ReadConfigText(string fileName)
        {
            try
            {
                string path = GetConfigFilePath(fileName);
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
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
                string path = GetConfigFilePath(fileName);
                return File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public static void WriteConfigText(string fileName, string contents)
        {
            string path = GetConfigFilePath(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ConfigDirectory);
            File.WriteAllText(path, contents ?? string.Empty);
        }

        public static void WriteConfigLines(string fileName, string[] lines)
        {
            string path = GetConfigFilePath(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ConfigDirectory);
            File.WriteAllLines(path, lines ?? Array.Empty<string>());
        }

        public static void AppendConfigLine(string fileName, string line)
        {
            string path = GetConfigFilePath(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ConfigDirectory);
            File.AppendAllText(path, string.Concat(line ?? string.Empty, Environment.NewLine));
        }

        public static OptionsData LoadOptions()
        {
            try
            {
                if (!File.Exists(OptionsPath))
                    return new OptionsData();

                string json = File.ReadAllText(OptionsPath);
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
            SaveFile(OptionsPath, options);
        }

        public static MedalProgress LoadMedals()
        {
            return LoadFile(MedalsPath, new MedalProgress());
        }

        public static void SaveMedals(MedalProgress medals)
        {
            SaveFile(MedalsPath, medals);
        }

        public static RunSaveData LoadRunSlot(int slotIndex)
        {
            return LoadFile<RunSaveData>(GetRunSlotPath(slotIndex), null);
        }

        public static void SaveRunSlot(int slotIndex, RunSaveData data)
        {
            if (data == null)
                return;

            SaveFile(GetRunSlotPath(slotIndex), data);
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

        private static T LoadFile<T>(string path, T fallback) where T : class
        {
            try
            {
                if (!File.Exists(path))
                    return fallback;

                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(json, jsonOptions) ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static void SaveFile<T>(string path, T value)
        {
            Directory.CreateDirectory(BaseDirectory);
            File.WriteAllText(path, JsonSerializer.Serialize(value, jsonOptions));
        }

        private static void EnsureInitialized()
        {
            if (initialized)
                return;

            initialized = true;
            Directory.CreateDirectory(documentsBaseDirectory);
            Directory.CreateDirectory(Path.Combine(documentsBaseDirectory, "config"));

            if (!Directory.Exists(legacyBaseDirectory))
                return;

            CopyMissingRecursive(legacyBaseDirectory, documentsBaseDirectory);
        }

        private static void CopyMissingRecursive(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (string sourceFile in Directory.GetFiles(sourceDirectory))
            {
                string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
                if (!File.Exists(destinationFile))
                    File.Copy(sourceFile, destinationFile);
            }

            foreach (string sourceSubdirectory in Directory.GetDirectories(sourceDirectory))
            {
                string destinationSubdirectory = Path.Combine(destinationDirectory, Path.GetFileName(sourceSubdirectory));
                CopyMissingRecursive(sourceSubdirectory, destinationSubdirectory);
            }
        }

        private static string SanitizeRelativeFileName(string fileName)
        {
            string safe = string.IsNullOrWhiteSpace(fileName) ? "default.cfg" : fileName.Trim();
            safe = safe.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            safe = safe.TrimStart(Path.DirectorySeparatorChar);
            return safe;
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
