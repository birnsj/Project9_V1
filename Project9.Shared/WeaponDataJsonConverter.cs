using System.Text.Json;
using System.Text.Json.Serialization;

namespace Project9.Shared
{
    /// <summary>
    /// Custom JSON converter for polymorphic WeaponData deserialization
    /// </summary>
    public class WeaponDataJsonConverter : JsonConverter<WeaponData>
    {
        public override WeaponData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                if (!doc.RootElement.TryGetProperty("type", out JsonElement typeElement))
                {
                    throw new JsonException("WeaponData missing 'type' property");
                }

                string type = typeElement.GetString() ?? "";
                
                // Create appropriate subclass based on type
                WeaponData? weaponData = type.ToLower() switch
                {
                    "sword" => new SwordData(),
                    "gun" => new GunData(),
                    _ => throw new JsonException($"Unknown weapon type: {type}")
                };

                // Deserialize common properties (X, Y) - try both camelCase and lowercase
                if (doc.RootElement.TryGetProperty("x", out JsonElement xElement) || 
                    doc.RootElement.TryGetProperty("X", out xElement))
                {
                    weaponData.X = xElement.GetSingle();
                }

                if (doc.RootElement.TryGetProperty("y", out JsonElement yElement) || 
                    doc.RootElement.TryGetProperty("Y", out yElement))
                {
                    weaponData.Y = yElement.GetSingle();
                }
                
                // Deserialize base weapon properties
                if (doc.RootElement.TryGetProperty("damage", out JsonElement damageElement))
                {
                    weaponData.Damage = damageElement.GetSingle();
                }
                
                if (doc.RootElement.TryGetProperty("name", out JsonElement nameElement))
                {
                    weaponData.Name = nameElement.GetString() ?? "";
                }
                
                if (doc.RootElement.TryGetProperty("weaponColorR", out JsonElement colorRElement))
                {
                    weaponData.WeaponColorR = colorRElement.GetInt32();
                }
                
                if (doc.RootElement.TryGetProperty("weaponColorG", out JsonElement colorGElement))
                {
                    weaponData.WeaponColorG = colorGElement.GetInt32();
                }
                
                if (doc.RootElement.TryGetProperty("weaponColorB", out JsonElement colorBElement))
                {
                    weaponData.WeaponColorB = colorBElement.GetInt32();
                }
                
                if (doc.RootElement.TryGetProperty("knockbackDuration", out JsonElement knockbackElement))
                {
                    weaponData.KnockbackDuration = knockbackElement.GetSingle();
                }
                
                // Deserialize gun-specific properties
                if (weaponData is GunData gunData)
                {
                    if (doc.RootElement.TryGetProperty("projectileSpeed", out JsonElement projectileSpeedElement))
                    {
                        gunData.ProjectileSpeed = projectileSpeedElement.GetSingle();
                    }
                    
                    if (doc.RootElement.TryGetProperty("fireRate", out JsonElement fireRateElement))
                    {
                        gunData.FireRate = fireRateElement.GetSingle();
                    }
                }

                return weaponData;
            }
        }

        public override void Write(Utf8JsonWriter writer, WeaponData value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            
            // Write type discriminator
            writer.WriteString("type", value.Type);
            
            // Write common properties
            writer.WriteNumber("x", value.X);
            writer.WriteNumber("y", value.Y);
            writer.WriteNumber("damage", value.Damage);
            writer.WriteString("name", value.Name);
            writer.WriteNumber("weaponColorR", value.WeaponColorR);
            writer.WriteNumber("weaponColorG", value.WeaponColorG);
            writer.WriteNumber("weaponColorB", value.WeaponColorB);
            writer.WriteNumber("knockbackDuration", value.KnockbackDuration);
            
            // Write gun-specific properties
            if (value is GunData gunData)
            {
                writer.WriteNumber("projectileSpeed", gunData.ProjectileSpeed);
                writer.WriteNumber("fireRate", gunData.FireRate);
            }
            
            writer.WriteEndObject();
        }
    }
}

