using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceBurst.RuntimeData
{
    public static class LevelSerializer
    {
        private static readonly JsonSerializerOptions jsonOptions = CreateOptions();

        public static JsonSerializerOptions JsonOptions
        {
            get { return jsonOptions; }
        }

        public static EnemyArchetypeCatalogDefinition DeserializeArchetypes(string json)
        {
            return JsonSerializer.Deserialize<EnemyArchetypeCatalogDefinition>(json, jsonOptions);
        }

        public static LevelDefinition DeserializeLevel(string json)
        {
            return JsonSerializer.Deserialize<LevelDefinition>(json, jsonOptions);
        }

        public static string SerializeArchetypes(EnemyArchetypeCatalogDefinition definition)
        {
            return JsonSerializer.Serialize(definition, jsonOptions);
        }

        public static string SerializeLevel(LevelDefinition definition)
        {
            return JsonSerializer.Serialize(definition, jsonOptions);
        }

        public static EnemyArchetypeCatalogDefinition LoadArchetypesFromFile(string path)
        {
            return DeserializeArchetypes(File.ReadAllText(path));
        }

        public static LevelDefinition LoadLevelFromFile(string path)
        {
            return DeserializeLevel(File.ReadAllText(path));
        }

        public static void SaveArchetypesToFile(string path, EnemyArchetypeCatalogDefinition definition)
        {
            File.WriteAllText(path, SerializeArchetypes(definition));
        }

        public static void SaveLevelToFile(string path, LevelDefinition definition)
        {
            File.WriteAllText(path, SerializeLevel(definition));
        }

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions();
            options.WriteIndented = true;
            options.PropertyNameCaseInsensitive = true;
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}
