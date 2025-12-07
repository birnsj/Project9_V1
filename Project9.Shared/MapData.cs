using System.Text.Json.Serialization;

namespace Project9.Shared
{
    /// <summary>
    /// Represents a single tile in the map for JSON serialization
    /// </summary>
    public class TileData
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("terrainType")]
        public TerrainType TerrainType { get; set; }
    }

    /// <summary>
    /// Complete map data structure for JSON serialization
    /// </summary>
    public class MapData
    {
        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("tiles")]
        public List<TileData> Tiles { get; set; } = new List<TileData>();

        /// <summary>
        /// Creates a default empty map with specified dimensions
        /// </summary>
        public static MapData CreateDefault(int width = 20, int height = 20)
        {
            var map = new MapData
            {
                Width = width,
                Height = height
            };

            // Initialize with all grass tiles
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    map.Tiles.Add(new TileData
                    {
                        X = x,
                        Y = y,
                        TerrainType = TerrainType.Grass
                    });
                }
            }

            return map;
        }

        /// <summary>
        /// Gets the tile at the specified coordinates
        /// </summary>
        public TileData? GetTile(int x, int y)
        {
            return Tiles.FirstOrDefault(t => t.X == x && t.Y == y);
        }

        /// <summary>
        /// Sets or creates a tile at the specified coordinates
        /// </summary>
        public void SetTile(int x, int y, TerrainType terrainType)
        {
            var existing = GetTile(x, y);
            if (existing != null)
            {
                existing.TerrainType = terrainType;
            }
            else
            {
                Tiles.Add(new TileData
                {
                    X = x,
                    Y = y,
                    TerrainType = terrainType
                });
            }
        }
    }
}
