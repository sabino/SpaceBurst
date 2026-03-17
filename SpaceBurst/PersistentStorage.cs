using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceBurst
{
    static class PersistentStorage
    {
        private static readonly JsonSerializerOptions jsonOptions = CreateOptions();

        private static string BaseDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SpaceBurst");
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

        public static OptionsData LoadOptions()
        {
            return LoadFile(OptionsPath, new OptionsData());
        }

        public static void SaveOptions(OptionsData options)
        {
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

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions();
            options.WriteIndented = true;
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}
