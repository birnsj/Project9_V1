using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;

namespace Project9
{
    /// <summary>
    /// Manages all rendering including map, entities, and UI
    /// </summary>
    public class RenderSystem
    {
        private GraphicsDevice _graphicsDevice;
        private SpriteBatch _spriteBatch;
        private IsometricMap _map;
        private Camera _camera;
        private SpriteFont? _uiFont;
        
        private bool _showGrid64x32 = false;
        private bool _showCollision = true;
        private bool _showCollisionSpheres = true; // Show collision spheres for entities
        private Texture2D? _gridLineTexture;
        private Texture2D? _collisionDiamondTexture;
        
        // Performance tracking
        private int _lastDrawCallCount = 0;

        public bool ShowGrid64x32
        {
            get => _showGrid64x32;
            set => _showGrid64x32 = value;
        }

        public bool ShowCollision
        {
            get => _showCollision;
            set => _showCollision = value;
        }
        
        public bool ShowCollisionSpheres
        {
            get => _showCollisionSpheres;
            set => _showCollisionSpheres = value;
        }
        
        public int LastDrawCallCount => _lastDrawCallCount;

        public RenderSystem(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, IsometricMap map, Camera camera, SpriteFont? uiFont)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _map = map;
            _camera = camera;
            _uiFont = uiFont;
        }

        /// <summary>
        /// Render everything
        /// </summary>
        public void Render(EntityManager entityManager, CollisionManager collisionManager, Desktop desktop)
        {
            _lastDrawCallCount = 0;
            _graphicsDevice.Clear(Color.DarkSlateGray);

            // World space rendering
            _spriteBatch.Begin(
                transformMatrix: _camera.GetTransform(),
                samplerState: SamplerState.PointClamp
            );

            _map.Draw(_spriteBatch);
            _lastDrawCallCount++;

            if (_showGrid64x32)
            {
                DrawGrid64x32(_spriteBatch);
                _lastDrawCallCount++;
            }

            // Draw enemies
            foreach (var enemy in entityManager.Enemies)
            {
                float effectiveRange = enemy.HasDetectedPlayer 
                    ? enemy.DetectionRange 
                    : (entityManager.Player.IsSneaking ? enemy.DetectionRange * 0.5f : enemy.DetectionRange);
                    
                enemy.DrawAggroRadius(_spriteBatch, effectiveRange);
                enemy.DrawSightCone(_spriteBatch);
                
                // Draw collision sphere if enabled
                if (_showCollisionSpheres)
                {
                    enemy.DrawCollisionSphere(_spriteBatch);
                }
                
                enemy.Draw(_spriteBatch);
                _lastDrawCallCount += _showCollisionSpheres ? 4 : 3; // Aggro, sight cone, collision sphere (optional), sprite
            }

            // Draw player
            if (_showCollisionSpheres)
            {
                entityManager.Player.DrawCollisionSphere(_spriteBatch);
            }
            entityManager.Player.Draw(_spriteBatch);
            _lastDrawCallCount += _showCollisionSpheres ? 2 : 1;

            // Draw collision cells
            if (_showCollision)
            {
                DrawCollisionCells(_spriteBatch, collisionManager);
                _lastDrawCallCount++;
            }

            _spriteBatch.End();

            // Screen space rendering (UI)
            if (_uiFont != null)
            {
                _spriteBatch.Begin();

                // Version number
                string versionText = "V002";
                Vector2 textSize = _uiFont.MeasureString(versionText);
                Vector2 position = new Vector2(
                    _graphicsDevice.Viewport.Width - textSize.X - 10,
                    _graphicsDevice.Viewport.Height - textSize.Y - 10
                );
                _spriteBatch.DrawString(_uiFont, versionText, position, Color.White);

                // Sneak indicator
                if (entityManager.Player.IsSneaking)
                {
                    string sneakText = "SNEAK";
                    Vector2 sneakTextSize = _uiFont.MeasureString(sneakText);
                    Vector2 sneakPosition = new Vector2(
                        _graphicsDevice.Viewport.Width / 2.0f - sneakTextSize.X / 2.0f,
                        50.0f
                    );
                    _spriteBatch.DrawString(_uiFont, sneakText, sneakPosition, Color.Purple);
                }

                _spriteBatch.End();
            }

            // Myra UI
            desktop.Render();
        }

        private void DrawGrid64x32(SpriteBatch spriteBatch)
        {
            const float gridX = 64.0f;
            
            if (_gridLineTexture == null)
            {
                _gridLineTexture = new Texture2D(_graphicsDevice, 1, 1);
                _gridLineTexture.SetData(new[] { new Color(100, 80, 80, 80) });
            }

            Vector2 topLeft = ScreenToWorld(Vector2.Zero);
            Vector2 bottomRight = ScreenToWorld(new Vector2(
                _graphicsDevice.Viewport.Width,
                _graphicsDevice.Viewport.Height
            ));

            float minX = topLeft.X - Project9.Shared.IsometricMath.TileWidth * 2;
            float maxX = bottomRight.X + Project9.Shared.IsometricMath.TileWidth * 2;
            float minY = topLeft.Y - Project9.Shared.IsometricMath.TileHeight * 2;
            float maxY = bottomRight.Y + Project9.Shared.IsometricMath.TileHeight * 2;

            var (minTileX, minTileY) = Project9.Shared.IsometricMath.ScreenToTile(minX, minY);
            var (maxTileX, maxTileY) = Project9.Shared.IsometricMath.ScreenToTile(maxX, maxY);

            minTileX -= 3;
            minTileY -= 3;
            maxTileX += 3;
            maxTileY += 3;

            const int gridCellsPerTile = (int)(Project9.Shared.IsometricMath.TileWidth / gridX);

            // Draw lines parallel to tile edges
            for (int tileX = minTileX; tileX <= maxTileX; tileX++)
            {
                for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                {
                    float cellProgress = gridCell / (float)gridCellsPerTile;
                    float offsetX = cellProgress * (Project9.Shared.IsometricMath.TileWidth / 2.0f);
                    float offsetY = cellProgress * (Project9.Shared.IsometricMath.TileHeight / 2.0f);

                    var (startX, startY) = Project9.Shared.IsometricMath.TileToScreen(tileX, minTileY);
                    startX += offsetX;
                    startY += offsetY;

                    var (endX, endY) = Project9.Shared.IsometricMath.TileToScreen(tileX, maxTileY);
                    endX += offsetX;
                    endY += offsetY;

                    if ((startY >= minY && startY <= maxY) || (endY >= minY && endY <= maxY) ||
                        (startY < minY && endY > maxY) || (startY > maxY && endY < minY))
                    {
                        DrawLine(spriteBatch, new Vector2(startX, startY), new Vector2(endX, endY), _gridLineTexture!);
                    }
                }
            }

            for (int tileY = minTileY; tileY <= maxTileY; tileY++)
            {
                for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                {
                    float cellProgress = gridCell / (float)gridCellsPerTile;
                    float offsetX = -cellProgress * (Project9.Shared.IsometricMath.TileWidth / 2.0f);
                    float offsetY = cellProgress * (Project9.Shared.IsometricMath.TileHeight / 2.0f);

                    var (startX, startY) = Project9.Shared.IsometricMath.TileToScreen(minTileX, tileY);
                    startX += offsetX;
                    startY += offsetY;

                    var (endX, endY) = Project9.Shared.IsometricMath.TileToScreen(maxTileX, tileY);
                    endX += offsetX;
                    endY += offsetY;

                    if ((startX >= minX && startX <= maxX) || (endX >= minX && endX <= maxX) ||
                        (startX < minX && endX > maxX) || (startX > maxX && endX < minX))
                    {
                        DrawLine(spriteBatch, new Vector2(startX, startY), new Vector2(endX, endY), _gridLineTexture!);
                    }
                }
            }
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Texture2D texture)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();

            spriteBatch.Draw(
                texture,
                start,
                null,
                Color.White,
                angle,
                Vector2.Zero,
                new Vector2(length, 1),
                SpriteEffects.None,
                0.0f
            );
        }

        private void DrawCollisionCells(SpriteBatch spriteBatch, CollisionManager collisionManager)
        {
            if (_collisionDiamondTexture == null)
            {
                CreateCollisionDiamondTexture();
            }

            foreach (var cell in collisionManager.GetCollisionCells())
            {
                Vector2 drawPosition = new Vector2(cell.X, cell.Y) - new Vector2(32, 16);
                spriteBatch.Draw(_collisionDiamondTexture, drawPosition, Color.White);
            }
        }

        private void CreateCollisionDiamondTexture()
        {
            int halfWidth = 32;
            int halfHeight = 16;
            int width = halfWidth * 2;
            int height = halfHeight * 2;
            
            _collisionDiamondTexture = new Texture2D(_graphicsDevice, width, height);
            Color[] colorData = new Color[width * height];
            
            Vector2 center = new Vector2(halfWidth, halfHeight);
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float dx = Math.Abs(x - center.X);
                    float dy = Math.Abs(y - center.Y);
                    float normalizedX = dx / halfWidth;
                    float normalizedY = dy / halfHeight;
                    
                    if (normalizedX + normalizedY <= 1.0f)
                    {
                        colorData[y * width + x] = new Color(128, 0, 128, 180);
                    }
                    else
                    {
                        colorData[y * width + x] = Color.Transparent;
                    }
                }
            }
            
            _collisionDiamondTexture.SetData(colorData);
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return screenPosition / _camera.Zoom + _camera.Position;
        }
    }
}

