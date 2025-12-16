using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Project9.Shared;

namespace Project9
{
    /// <summary>
    /// Represents a furniture/world object in the game world
    /// </summary>
    public class WorldObject
    {
        private Vector2 _position;
        private float _zHeight;
        private int _diamondWidth;
        private int _diamondHeight;
        private Color _boundingBoxColor;
        private float _boundingBoxOpacity;
        private bool _showBoundingBox;
        private string _name;
        private Texture2D? _diamondTexture;
        
        public Vector2 Position => _position;
        public float ZHeight => _zHeight;
        public int DiamondWidth => _diamondWidth;
        public int DiamondHeight => _diamondHeight;
        public string Name => _name;
        public Color BoundingBoxColor => _boundingBoxColor;
        public float BoundingBoxOpacity => _boundingBoxOpacity;
        public bool ShowBoundingBox
        {
            get => _showBoundingBox;
            set => _showBoundingBox = value;
        }
        
        public WorldObject(Project9.Shared.WorldObject data)
        {
            _position = new Vector2(data.X, data.Y);
            _zHeight = data.ZHeight;
            _diamondWidth = data.DiamondWidth;
            _diamondHeight = data.DiamondHeight;
            _boundingBoxColor = new Color(data.BoundingBoxColorR, data.BoundingBoxColorG, data.BoundingBoxColorB);
            _boundingBoxOpacity = data.BoundingBoxOpacity;
            _name = data.Name ?? "";
            _showBoundingBox = true; // Default to showing bounding box
        }
        
        /// <summary>
        /// Create diamond texture for isometric rendering
        /// </summary>
        public void CreateDiamondTexture(GraphicsDevice graphicsDevice)
        {
            if (_diamondTexture != null)
                return;
                
            int halfWidth = _diamondWidth / 2;
            int halfHeight = _diamondHeight / 2;
            int width = _diamondWidth;
            int height = _diamondHeight;
            
            _diamondTexture = new Texture2D(graphicsDevice, width, height);
            Color[] colorData = new Color[width * height];
            
            Vector2 center = new Vector2(halfWidth, halfHeight);
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float dx = System.Math.Abs(x - center.X);
                    float dy = System.Math.Abs(y - center.Y);
                    float normalizedX = dx / halfWidth;
                    float normalizedY = dy / halfHeight;
                    
                    if (normalizedX + normalizedY <= 1.0f)
                    {
                        colorData[y * width + x] = Color.White;
                    }
                    else
                    {
                        colorData[y * width + x] = Color.Transparent;
                    }
                }
            }
            
            _diamondTexture.SetData(colorData);
        }
        
        /// <summary>
        /// Draw the world object
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Texture2D? lineTexture)
        {
            if (!_showBoundingBox)
                return;
                
            if (_diamondTexture == null)
                return;
                
            // Draw the diamond shape with the bounding box color
            Color drawColor = new Color(
                _boundingBoxColor.R,
                _boundingBoxColor.G,
                _boundingBoxColor.B,
                (byte)(255 * _boundingBoxOpacity)
            );
            
            // Draw filled diamond
            spriteBatch.Draw(
                _diamondTexture,
                _position,
                null,
                drawColor,
                0f,
                new Vector2(_diamondWidth / 2.0f, _diamondHeight / 2.0f),
                1.0f,
                SpriteEffects.None,
                0f
            );
            
            // Draw wireframe outline if we have a line texture
            if (lineTexture != null)
            {
                DrawBoundingBox3D(spriteBatch, lineTexture);
            }
        }
        
        private void DrawBoundingBox3D(SpriteBatch spriteBatch, Texture2D lineTexture)
        {
            // Calculate isometric diamond corners
            float halfWidth = _diamondWidth / 2.0f;
            float halfHeight = _diamondHeight / 2.0f;
            
            Vector2 top = _position + new Vector2(0, -halfHeight);
            Vector2 right = _position + new Vector2(halfWidth, 0);
            Vector2 bottom = _position + new Vector2(0, halfHeight);
            Vector2 left = _position + new Vector2(-halfWidth, 0);
            
            Color boxColor = new Color(
                _boundingBoxColor.R,
                _boundingBoxColor.G,
                _boundingBoxColor.B,
                (byte)(255 * _boundingBoxOpacity)
            );
            
            // Draw diamond outline (4 lines)
            DrawLine(spriteBatch, lineTexture, top, right, boxColor, 2.0f);
            DrawLine(spriteBatch, lineTexture, right, bottom, boxColor, 2.0f);
            DrawLine(spriteBatch, lineTexture, bottom, left, boxColor, 2.0f);
            DrawLine(spriteBatch, lineTexture, left, top, boxColor, 2.0f);
            
            // Draw vertical lines for 3D effect (if zHeight > 0)
            if (_zHeight > 0)
            {
                Vector2 topTop = top + new Vector2(0, -_zHeight * 0.5f);
                Vector2 rightTop = right + new Vector2(0, -_zHeight * 0.5f);
                Vector2 bottomTop = bottom + new Vector2(0, -_zHeight * 0.5f);
                Vector2 leftTop = left + new Vector2(0, -_zHeight * 0.5f);
                
                // Draw top diamond
                DrawLine(spriteBatch, lineTexture, topTop, rightTop, boxColor, 2.0f);
                DrawLine(spriteBatch, lineTexture, rightTop, bottomTop, boxColor, 2.0f);
                DrawLine(spriteBatch, lineTexture, bottomTop, leftTop, boxColor, 2.0f);
                DrawLine(spriteBatch, lineTexture, leftTop, topTop, boxColor, 2.0f);
                
                // Draw vertical edges
                DrawLine(spriteBatch, lineTexture, top, topTop, boxColor, 2.0f);
                DrawLine(spriteBatch, lineTexture, right, rightTop, boxColor, 2.0f);
                DrawLine(spriteBatch, lineTexture, bottom, bottomTop, boxColor, 2.0f);
                DrawLine(spriteBatch, lineTexture, left, leftTop, boxColor, 2.0f);
            }
        }
        
        private void DrawLine(SpriteBatch spriteBatch, Texture2D texture, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 edge = end - start;
            float angle = (float)System.Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();
            
            spriteBatch.Draw(
                texture,
                start,
                null,
                color,
                angle,
                Vector2.Zero,
                new Vector2(length, thickness),
                SpriteEffects.None,
                0.0f
            );
        }
    }
}

