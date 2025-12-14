using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Project9.Shared;

namespace Project9.Editor
{
    public class TileTextureLoader
    {
        private readonly Dictionary<TerrainType, Bitmap> _textures;

        public TileTextureLoader()
        {
            _textures = new Dictionary<TerrainType, Bitmap>();
        }

        public void LoadTextures()
        {
            foreach (TerrainType terrainType in Enum.GetValues<TerrainType>())
            {
                string texturePath;
                // Test tiles are in the test folder, others are in template folder
                if (terrainType == TerrainType.Test || terrainType == TerrainType.Test2)
                {
                    texturePath = $"Content/sprites/tiles/test/{terrainType}.png";
                }
                else
                {
                    texturePath = $"Content/sprites/tiles/template/{terrainType}.png";
                }
                
                string? resolvedPath = ResolveTexturePath(texturePath);
                
                if (resolvedPath != null && File.Exists(resolvedPath))
                {
                    try
                    {
                        // Load image preserving alpha channel for transparency support
                        using (Image originalImage = Image.FromFile(resolvedPath))
                        {
                            // Create bitmap with 32-bit ARGB format to preserve alpha channel
                            Bitmap bitmap = new Bitmap(originalImage.Width, originalImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            using (Graphics g = Graphics.FromImage(bitmap))
                            {
                                g.Clear(Color.Transparent); // Clear with transparent background
                                g.DrawImage(originalImage, 0, 0); // Draw the original image (preserves alpha)
                            }
                            _textures[terrainType] = bitmap;
                            Console.WriteLine($"[TileTextureLoader] Loaded {terrainType} from {resolvedPath} (Format: {bitmap.PixelFormat})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TileTextureLoader] Failed to load {terrainType} from {resolvedPath}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[TileTextureLoader] Texture not found: {texturePath}");
                }
            }
        }

        public Bitmap? GetTexture(TerrainType terrainType)
        {
            return _textures.TryGetValue(terrainType, out var texture) ? texture : null;
        }

        private static string? ResolveTexturePath(string relativePath)
        {
            // Try current directory first
            string currentDir = Directory.GetCurrentDirectory();
            string fullPath = Path.GetFullPath(Path.Combine(currentDir, relativePath));
            if (File.Exists(fullPath))
                return fullPath;

            // Try executable directory
            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            fullPath = Path.GetFullPath(Path.Combine(exeDir, relativePath));
            if (File.Exists(fullPath))
                return fullPath;

            // Try going up to project root (for development)
            var dir = new DirectoryInfo(exeDir);
            while (dir != null && dir.Parent != null)
            {
                string testPath = Path.GetFullPath(Path.Combine(dir.FullName, relativePath));
                if (File.Exists(testPath))
                    return testPath;
                dir = dir.Parent;
            }

            return null;
        }

        public void Dispose()
        {
            foreach (var texture in _textures.Values)
            {
                texture?.Dispose();
            }
            _textures.Clear();
        }
    }
}


