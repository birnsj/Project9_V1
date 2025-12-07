using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    public class IsometricTile
    {
        public static int TileWidth = 1024;
        public static int TileHeight = 512;

        public int TileX { get; set; }
        public int TileY { get; set; }
        public Texture2D Texture { get; set; }
        public Color TintColor { get; set; }

        public IsometricTile(int tileX, int tileY, Texture2D texture)
        {
            TileX = tileX;
            TileY = tileY;
            Texture = texture;
            TintColor = Color.White;
        }

        public Vector2 GetScreenPosition()
        {
            float screenX = (TileX - TileY) * (TileWidth / 2.0f);
            float screenY = (TileX + TileY) * (TileHeight / 2.0f);
            return new Vector2(screenX, screenY);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (Texture != null)
            {
                Vector2 position = GetScreenPosition();
                spriteBatch.Draw(Texture, position, TintColor);
            }
        }
    }
}
