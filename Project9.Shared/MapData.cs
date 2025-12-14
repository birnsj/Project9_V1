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
    /// Represents an enemy spawn position in the map for JSON serialization
    /// </summary>
    public class EnemyData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }
        
        // Legacy support: if X/Y are integers, treat as tile coordinates
        [JsonIgnore]
        public int TileX => (int)Math.Round(X);
        
        [JsonIgnore]
        public int TileY => (int)Math.Round(Y);
        
        // Enemy AI properties (all optional with defaults)
        [JsonPropertyName("attackRange")]
        public float AttackRange { get; set; } = 50.0f;
        
        [JsonPropertyName("detectionRange")]
        public float DetectionRange { get; set; } = 200.0f;
        
        [JsonPropertyName("attackCooldown")]
        public float AttackCooldown { get; set; } = 1.0f;
        
        [JsonPropertyName("chaseSpeed")]
        public float ChaseSpeed { get; set; } = 100.0f;
        
        [JsonPropertyName("maxHealth")]
        public float MaxHealth { get; set; } = 50.0f;
        
        [JsonPropertyName("sightConeAngle")]
        public float SightConeAngle { get; set; } = 60.0f; // Degrees
        
        [JsonPropertyName("sightConeLength")]
        public float SightConeLength { get; set; } = -1.0f; // -1 means use DetectionRange * 0.8
        
        [JsonPropertyName("rotationSpeed")]
        public float RotationSpeed { get; set; } = 45.0f; // Degrees per second
        
        [JsonPropertyName("exclamationDuration")]
        public float ExclamationDuration { get; set; } = 1.0f;
        
        [JsonPropertyName("outOfRangeThreshold")]
        public float OutOfRangeThreshold { get; set; } = 3.0f;
        
        [JsonPropertyName("searchDuration")]
        public float SearchDuration { get; set; } = 5.0f;
        
        [JsonPropertyName("searchRadius")]
        public float SearchRadius { get; set; } = 200.0f;
        
        [JsonPropertyName("maxChaseRange")]
        public float MaxChaseRange { get; set; } = 1024.0f;
        
        [JsonPropertyName("initialRotation")]
        public float InitialRotation { get; set; } = -1.0f; // -1 means random
        
        [JsonPropertyName("rotation")]
        public float Rotation { get; set; } = 0.0f; // Current rotation in radians
    }

    /// <summary>
    /// Represents the player spawn position in the map for JSON serialization
    /// </summary>
    public class PlayerData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }
        
        // Legacy support: if X/Y are integers, treat as tile coordinates
        [JsonIgnore]
        public int TileX => (int)Math.Round(X);
        
        [JsonIgnore]
        public int TileY => (int)Math.Round(Y);
        
        // Player movement properties (all optional with defaults)
        [JsonPropertyName("walkSpeed")]
        public float WalkSpeed { get; set; } = 75.0f;
        
        [JsonPropertyName("runSpeed")]
        public float RunSpeed { get; set; } = 150.0f;
        
        [JsonPropertyName("maxHealth")]
        public float MaxHealth { get; set; } = 100.0f;
        
        [JsonPropertyName("sneakSpeedMultiplier")]
        public float SneakSpeedMultiplier { get; set; } = 0.5f;
        
        [JsonPropertyName("respawnCountdown")]
        public float RespawnCountdown { get; set; } = 10.0f;
        
        [JsonPropertyName("deathPulseSpeed")]
        public float DeathPulseSpeed { get; set; } = 2.0f; // Pulses per second
        
        [JsonPropertyName("stopThreshold")]
        public float StopThreshold { get; set; } = 1.0f;
        
        [JsonPropertyName("slowdownRadius")]
        public float SlowdownRadius { get; set; } = 50.0f;
        
        [JsonPropertyName("sneakStopThreshold")]
        public float SneakStopThreshold { get; set; } = 10.0f;
        
        [JsonPropertyName("runStopThreshold")]
        public float RunStopThreshold { get; set; } = 5.0f;
        
        [JsonPropertyName("attackDamage")]
        public float AttackDamage { get; set; } = 10.0f;
        
        [JsonPropertyName("rotation")]
        public float Rotation { get; set; } = 0.0f; // Current rotation in radians
    }

    /// <summary>
    /// Represents a security camera position in the map for JSON serialization
    /// Extends EnemyData to inherit enemy properties, but cameras have additional camera-specific properties
    /// </summary>
    public class CameraData : EnemyData
    {
        // Camera-specific properties (all optional with defaults)
        [JsonPropertyName("sweepAngle")]
        public float SweepAngle { get; set; } = 90.0f; // Sweep angle in degrees (default 90)
        
        [JsonPropertyName("cameraRotationSpeed")]
        public float CameraRotationSpeed { get; set; } = 30.0f; // Rotation speed in degrees per second
        
        [JsonPropertyName("pauseDuration")]
        public float PauseDuration { get; set; } = 1.5f; // Pause duration at endpoints in seconds
        
        [JsonPropertyName("alertRadius")]
        public float AlertRadius { get; set; } = 1024.0f; // Radius to alert enemies in pixels
        
        [JsonPropertyName("alertCooldown")]
        public float AlertCooldown { get; set; } = 2.0f; // Cooldown between alerts in seconds
        
        [JsonPropertyName("cameraSightConeLength")]
        public float CameraSightConeLength { get; set; } = -1.0f; // -1 means use DetectionRange
    }

    /// <summary>
    /// Base class for weapon spawn positions in the map for JSON serialization
    /// </summary>
    public abstract class WeaponData
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }
        
        /// <summary>
        /// Get the weapon type name for serialization
        /// </summary>
        [JsonPropertyName("type")]
        public abstract string Type { get; }
        
        // Base weapon properties
        [JsonPropertyName("damage")]
        public float Damage { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("weaponColorR")]
        public int WeaponColorR { get; set; }
        
        [JsonPropertyName("weaponColorG")]
        public int WeaponColorG { get; set; }
        
        [JsonPropertyName("weaponColorB")]
        public int WeaponColorB { get; set; }
        
        [JsonPropertyName("knockbackDuration")]
        public float KnockbackDuration { get; set; }
    }

    /// <summary>
    /// Represents a sword weapon spawn position
    /// </summary>
    public class SwordData : WeaponData
    {
        public override string Type => "sword";
        
        public SwordData()
        {
            // Default values for sword
            Damage = 20.0f;
            Name = "Sword";
            WeaponColorR = 192; // Silver color RGB
            WeaponColorG = 192;
            WeaponColorB = 192;
            KnockbackDuration = 0.5f;
        }
    }

    /// <summary>
    /// Represents a gun weapon spawn position
    /// </summary>
    public class GunData : WeaponData
    {
        public override string Type => "gun";
        
        // Gun-specific properties
        [JsonPropertyName("projectileSpeed")]
        public float ProjectileSpeed { get; set; }
        
        [JsonPropertyName("fireRate")]
        public float FireRate { get; set; }
        
        public GunData()
        {
            // Default values for gun
            Damage = 15.0f;
            Name = "Gun";
            WeaponColorR = 169; // DarkGray color RGB
            WeaponColorG = 169;
            WeaponColorB = 169;
            KnockbackDuration = 0.4f;
            ProjectileSpeed = 500.0f;
            FireRate = 6.0f; // 6 shots per second
        }
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

        [JsonPropertyName("enemies")]
        public List<EnemyData> Enemies { get; set; } = new List<EnemyData>();

        [JsonPropertyName("cameras")]
        public List<CameraData> Cameras { get; set; } = new List<CameraData>();

        [JsonPropertyName("weapons")]
        public List<WeaponData> Weapons { get; set; } = new List<WeaponData>();

        [JsonPropertyName("player")]
        public PlayerData? Player { get; set; }

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

            // Set default player position at map center (in pixel coordinates)
            float centerTileX = (width - 1) / 2.0f;
            float centerTileY = (height - 1) / 2.0f;
            var (centerScreenX, centerScreenY) = IsometricMath.TileToScreen((int)centerTileX, (int)centerTileY);
            map.Player = new PlayerData
            {
                X = centerScreenX,
                Y = centerScreenY
            };

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
