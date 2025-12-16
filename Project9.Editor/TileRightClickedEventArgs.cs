using Project9.Shared;

namespace Project9.Editor
{
    public class TileRightClickedEventArgs : EventArgs
    {
        public TerrainType TerrainType { get; }

        public TileRightClickedEventArgs(TerrainType terrainType)
        {
            TerrainType = terrainType;
        }
    }
}







