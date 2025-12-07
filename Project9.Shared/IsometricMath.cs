namespace Project9.Shared
{
    /// <summary>
    /// Utility class for isometric coordinate conversions
    /// </summary>
    public static class IsometricMath
    {
        public const int TileWidth = 1024;
        public const int TileHeight = 512;

        /// <summary>
        /// Convert tile coordinates (x, y) to screen position (pixels)
        /// </summary>
        public static (float screenX, float screenY) TileToScreen(int tileX, int tileY)
        {
            float screenX = (tileX - tileY) * (TileWidth / 2.0f);
            float screenY = (tileX + tileY) * (TileHeight / 2.0f);
            return (screenX, screenY);
        }

        /// <summary>
        /// Convert screen coordinates to tile coordinates (approximate)
        /// Uses inverse isometric projection formula
        /// </summary>
        public static (int tileX, int tileY) ScreenToTile(float screenX, float screenY)
        {
            // Inverse isometric projection:
            // screenX = (tileX - tileY) * (TileWidth / 2)
            // screenY = (tileX + tileY) * (TileHeight / 2)
            // Solving for tileX and tileY:
            float tileX = (screenX / (TileWidth / 2.0f) + screenY / (TileHeight / 2.0f)) / 2.0f;
            float tileY = (screenY / (TileHeight / 2.0f) - screenX / (TileWidth / 2.0f)) / 2.0f;
            
            return ((int)Math.Round(tileX), (int)Math.Round(tileY));
        }

        /// <summary>
        /// Get the bounding rectangle for a tile in screen coordinates
        /// </summary>
        public static (float x, float y, float width, float height) GetTileBounds(int tileX, int tileY)
        {
            var (screenX, screenY) = TileToScreen(tileX, tileY);
            return (screenX, screenY, TileWidth, TileHeight);
        }
    }
}
