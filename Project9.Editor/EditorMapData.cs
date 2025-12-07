using System;
using System.IO;
using System.Threading.Tasks;
using Project9.Shared;

namespace Project9.Editor
{
    public class EditorMapData
    {
        private MapData _mapData;
        private string _filePath;

        public MapData MapData => _mapData;
        public string FilePath => _filePath;
        public int Width => _mapData.Width;
        public int Height => _mapData.Height;

        public EditorMapData()
        {
            _mapData = new MapData();
            _filePath = "Content/world/world.json";
        }

        public async Task LoadAsync(string? filePath = null)
        {
            string pathToLoad = filePath ?? _filePath;
            string? resolvedPath = ResolveMapPath(pathToLoad);
            
            if (resolvedPath == null)
            {
                resolvedPath = pathToLoad;
            }

            if (File.Exists(resolvedPath))
            {
                try
                {
                    var loadedData = await MapSerializer.LoadFromFileAsync(resolvedPath);
                    if (loadedData != null)
                    {
                        _mapData = loadedData;
                        _filePath = resolvedPath;
                        Console.WriteLine($"[EditorMapData] Loaded map from {resolvedPath}: {_mapData.Width}x{_mapData.Height}, {_mapData.Tiles.Count} tiles");
                    }
                    else
                    {
                        Console.WriteLine($"[EditorMapData] Failed to load map from {resolvedPath}, creating default map");
                        CreateDefaultMap();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EditorMapData] Error loading map from {resolvedPath}: {ex.Message}");
                    CreateDefaultMap();
                }
            }
            else
            {
                Console.WriteLine($"[EditorMapData] Map file not found at {resolvedPath}, creating default map");
                CreateDefaultMap();
            }
        }

        public async Task SaveAsync(string? filePath = null)
        {
            string pathToSave = filePath ?? _filePath;
            await MapSerializer.SaveToFileAsync(_mapData, pathToSave);
            _filePath = pathToSave;
            Console.WriteLine($"[EditorMapData] Saved map to {pathToSave}");
        }

        public TileData? GetTile(int x, int y)
        {
            return _mapData.GetTile(x, y);
        }

        public void SetTile(int x, int y, TerrainType terrainType)
        {
            _mapData.SetTile(x, y, terrainType);
        }

        private void CreateDefaultMap()
        {
            _mapData = MapData.CreateDefault(20, 20);
        }

        private static string? ResolveMapPath(string relativePath)
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
    }
}


