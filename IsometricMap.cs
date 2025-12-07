using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    public enum TerrainType
    {
        Grass,
        Water,
        Stone,
        Sand,
        Forest,
        Dirt
    }

    public class IsometricMap
    {
        private const int MapWidth = 20;
        private const int MapHeight = 20;
        private List<IsometricTile> _tiles;
        private Dictionary<TerrainType, Texture2D> _terrainTextures;

        public IsometricMap(GraphicsDevice graphicsDevice)
        {
            _tiles = new List<IsometricTile>();
            _terrainTextures = new Dictionary<TerrainType, Texture2D>();
            GenerateMap(graphicsDevice);
        }

        private void GenerateMap(GraphicsDevice graphicsDevice)
        {
            // Create terrain textures
            _terrainTextures[TerrainType.Grass] = CreateGrassTexture(graphicsDevice);
            _terrainTextures[TerrainType.Water] = CreateWaterTexture(graphicsDevice);
            _terrainTextures[TerrainType.Stone] = CreateStoneTexture(graphicsDevice);
            _terrainTextures[TerrainType.Sand] = CreateSandTexture(graphicsDevice);
            _terrainTextures[TerrainType.Forest] = CreateForestTexture(graphicsDevice);
            _terrainTextures[TerrainType.Dirt] = CreateDirtTexture(graphicsDevice);

            // Generate tiles with varied terrain
            Random random = new Random(42); // Fixed seed for consistency
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    // Create varied terrain distribution
                    TerrainType terrain = DetermineTerrainType(x, y, random);
                    var tile = new IsometricTile(x, y, _terrainTextures[terrain])
                    {
                        TintColor = Color.White
                    };
                    _tiles.Add(tile);
                }
            }
        }

        private TerrainType DetermineTerrainType(int x, int y, Random random)
        {
            // Create interesting terrain distribution
            float noiseX = x / 5.0f;
            float noiseY = y / 5.0f;
            
            // Use some simple patterns for terrain generation
            float value = (float)Math.Sin(noiseX) * (float)Math.Cos(noiseY) + random.NextSingle() * 0.3f;
            
            // Add water areas
            if (x < 3 || x > MapWidth - 4 || y < 3 || y > MapHeight - 4)
            {
                if (random.NextSingle() < 0.6f)
                    return TerrainType.Water;
            }
            
            // Center area variations
            if (value > 0.4f)
                return TerrainType.Grass;
            else if (value > 0.1f)
                return TerrainType.Forest;
            else if (value > -0.2f)
                return TerrainType.Dirt;
            else if (value > -0.5f)
                return TerrainType.Sand;
            else
                return TerrainType.Stone;
        }

        private Texture2D CreateGrassTexture(GraphicsDevice graphicsDevice)
        {
            int width = IsometricTile.TileWidth;
            int height = IsometricTile.TileHeight;

            Texture2D texture = new Texture2D(graphicsDevice, width, height);
            Color[] data = new Color[width * height];

            Random random = new Random(1);
            int centerX = width / 2;
            int centerY = height / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    float dx = Math.Abs(x - centerX);
                    float dy = Math.Abs(y - centerY);

                    if ((dx / (width / 2.0f)) + (dy / (height / 2.0f)) <= 1.0f)
                    {
                        // Bright green grass with texture variation
                        float dist = (dx + dy) / (width / 2.0f + height / 2.0f);
                        float noise = (float)random.NextDouble() * 0.15f;
                        float r = 0.2f + noise;
                        float g = 0.7f + noise * 0.5f;
                        float b = 0.3f + noise * 0.3f;
                        float brightness = 1.0f - dist * 0.3f;
                        data[index] = new Color(r * brightness, g * brightness, b * brightness);
                    }
                    else
                    {
                        data[index] = Color.Transparent;
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }

        private Texture2D CreateWaterTexture(GraphicsDevice graphicsDevice)
        {
            int width = IsometricTile.TileWidth;
            int height = IsometricTile.TileHeight;

            Texture2D texture = new Texture2D(graphicsDevice, width, height);
            Color[] data = new Color[width * height];

            Random random = new Random(2);
            int centerX = width / 2;
            int centerY = height / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    float dx = Math.Abs(x - centerX);
                    float dy = Math.Abs(y - centerY);

                    if ((dx / (width / 2.0f)) + (dy / (height / 2.0f)) <= 1.0f)
                    {
                        // Bright blue water with wave-like patterns
                        float dist = (dx + dy) / (width / 2.0f + height / 2.0f);
                        float wave = (float)Math.Sin(x * 0.1f) * (float)Math.Cos(y * 0.1f) * 0.1f;
                        float r = 0.2f + wave;
                        float g = 0.5f + wave * 0.5f;
                        float b = 0.9f + wave * 0.3f;
                        float brightness = 1.0f - dist * 0.2f;
                        data[index] = new Color(r * brightness, g * brightness, b * brightness);
                    }
                    else
                    {
                        data[index] = Color.Transparent;
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }

        private Texture2D CreateStoneTexture(GraphicsDevice graphicsDevice)
        {
            int width = IsometricTile.TileWidth;
            int height = IsometricTile.TileHeight;

            Texture2D texture = new Texture2D(graphicsDevice, width, height);
            Color[] data = new Color[width * height];

            Random random = new Random(3);
            int centerX = width / 2;
            int centerY = height / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    float dx = Math.Abs(x - centerX);
                    float dy = Math.Abs(y - centerY);

                    if ((dx / (width / 2.0f)) + (dy / (height / 2.0f)) <= 1.0f)
                    {
                        // Bright gray stone with texture
                        float dist = (dx + dy) / (width / 2.0f + height / 2.0f);
                        float noise = (float)random.NextDouble() * 0.2f;
                        float gray = 0.6f + noise;
                        float brightness = 1.0f - dist * 0.25f;
                        data[index] = new Color(gray * brightness, gray * brightness, gray * brightness);
                    }
                    else
                    {
                        data[index] = Color.Transparent;
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }

        private Texture2D CreateSandTexture(GraphicsDevice graphicsDevice)
        {
            int width = IsometricTile.TileWidth;
            int height = IsometricTile.TileHeight;

            Texture2D texture = new Texture2D(graphicsDevice, width, height);
            Color[] data = new Color[width * height];

            Random random = new Random(4);
            int centerX = width / 2;
            int centerY = height / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    float dx = Math.Abs(x - centerX);
                    float dy = Math.Abs(y - centerY);

                    if ((dx / (width / 2.0f)) + (dy / (height / 2.0f)) <= 1.0f)
                    {
                        // Bright yellow/beige sand
                        float dist = (dx + dy) / (width / 2.0f + height / 2.0f);
                        float noise = (float)random.NextDouble() * 0.15f;
                        float r = 0.85f + noise;
                        float g = 0.75f + noise;
                        float b = 0.5f + noise * 0.5f;
                        float brightness = 1.0f - dist * 0.2f;
                        data[index] = new Color(r * brightness, g * brightness, b * brightness);
                    }
                    else
                    {
                        data[index] = Color.Transparent;
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }

        private Texture2D CreateForestTexture(GraphicsDevice graphicsDevice)
        {
            int width = IsometricTile.TileWidth;
            int height = IsometricTile.TileHeight;

            Texture2D texture = new Texture2D(graphicsDevice, width, height);
            Color[] data = new Color[width * height];

            Random random = new Random(5);
            int centerX = width / 2;
            int centerY = height / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    float dx = Math.Abs(x - centerX);
                    float dy = Math.Abs(y - centerY);

                    if ((dx / (width / 2.0f)) + (dy / (height / 2.0f)) <= 1.0f)
                    {
                        // Dark green forest
                        float dist = (dx + dy) / (width / 2.0f + height / 2.0f);
                        float noise = (float)random.NextDouble() * 0.2f;
                        float r = 0.1f + noise * 0.2f;
                        float g = 0.5f + noise * 0.3f;
                        float b = 0.2f + noise * 0.2f;
                        float brightness = 1.0f - dist * 0.25f;
                        data[index] = new Color(r * brightness, g * brightness, b * brightness);
                    }
                    else
                    {
                        data[index] = Color.Transparent;
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }

        private Texture2D CreateDirtTexture(GraphicsDevice graphicsDevice)
        {
            int width = IsometricTile.TileWidth;
            int height = IsometricTile.TileHeight;

            Texture2D texture = new Texture2D(graphicsDevice, width, height);
            Color[] data = new Color[width * height];

            Random random = new Random(6);
            int centerX = width / 2;
            int centerY = height / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    float dx = Math.Abs(x - centerX);
                    float dy = Math.Abs(y - centerY);

                    if ((dx / (width / 2.0f)) + (dy / (height / 2.0f)) <= 1.0f)
                    {
                        // Brown dirt
                        float dist = (dx + dy) / (width / 2.0f + height / 2.0f);
                        float noise = (float)random.NextDouble() * 0.15f;
                        float r = 0.5f + noise * 0.3f;
                        float g = 0.35f + noise * 0.2f;
                        float b = 0.2f + noise * 0.1f;
                        float brightness = 1.0f - dist * 0.25f;
                        data[index] = new Color(r * brightness, g * brightness, b * brightness);
                    }
                    else
                    {
                        data[index] = Color.Transparent;
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Draw tiles in proper order (back to front)
            // Sort by screen Y position to ensure correct depth
            var sortedTiles = new List<IsometricTile>(_tiles);
            sortedTiles.Sort((a, b) =>
            {
                Vector2 posA = a.GetScreenPosition();
                Vector2 posB = b.GetScreenPosition();
                int compareY = posA.Y.CompareTo(posB.Y);
                if (compareY != 0) return compareY;
                return posA.X.CompareTo(posB.X);
            });

            foreach (var tile in sortedTiles)
            {
                tile.Draw(spriteBatch);
            }
        }

        public Vector2 GetMapCenter()
        {
            // Calculate center of the map in screen coordinates
            float centerTileX = (MapWidth - 1) / 2.0f;
            float centerTileY = (MapHeight - 1) / 2.0f;
            float centerScreenX = (centerTileX - centerTileY) * (IsometricTile.TileWidth / 2.0f);
            float centerScreenY = (centerTileX + centerTileY) * (IsometricTile.TileHeight / 2.0f);
            return new Vector2(centerScreenX, centerScreenY);
        }
    }
}
