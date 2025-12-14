using System.Text.Json;
using System.Text.Json.Serialization;

namespace Project9.Shared
{
    /// <summary>
    /// Handles serialization and deserialization of map data to/from JSON
    /// </summary>
    public static class MapSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { 
                new JsonStringEnumConverter(allowIntegerValues: true),
                new WeaponDataJsonConverter()
            }
        };

        /// <summary>
        /// Serialize map data to JSON string
        /// </summary>
        public static string Serialize(MapData mapData)
        {
            return JsonSerializer.Serialize(mapData, Options);
        }

        /// <summary>
        /// Deserialize JSON string to map data
        /// </summary>
        public static MapData? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<MapData>(json, Options);
        }

        /// <summary>
        /// Save map data to a file
        /// </summary>
        public static async Task SaveToFileAsync(MapData mapData, string filePath)
        {
            string json = Serialize(mapData);
            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>
        /// Load map data from a file
        /// </summary>
        public static async Task<MapData?> LoadFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(filePath);
            return Deserialize(json);
        }
    }
}
