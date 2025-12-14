using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    /// <summary>
    /// Base class for all game entities with position, movement, health, and rendering
    /// </summary>
    public abstract class Entity
    {
        // ===== POSITION =====
        protected Vector2 _position;
        protected float _zHeight = 0.0f; // Z height for 3D isometric bounding boxes
        
        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }
        
        public float ZHeight
        {
            get => _zHeight;
            set => _zHeight = Math.Max(0.0f, value); // Ensure non-negative
        }
        
        // ===== 3D BOUNDING BOX =====
        protected float _boundingBoxWidth = 64.0f;
        protected float _boundingBoxHeight = 64.0f;
        protected float _boundingBoxDepth = 64.0f;
        protected Texture2D? _boundingBoxTexture;
        protected bool _showBoundingBox = false;
        
        public float BoundingBoxWidth
        {
            get => _boundingBoxWidth;
            set => _boundingBoxWidth = value;
        }
        
        public float BoundingBoxHeight
        {
            get => _boundingBoxHeight;
            set => _boundingBoxHeight = value;
        }
        
        public float BoundingBoxDepth
        {
            get => _boundingBoxDepth;
            set => _boundingBoxDepth = value;
        }
        
        public bool ShowBoundingBox
        {
            get => _showBoundingBox;
            set => _showBoundingBox = value;
        }

        // ===== MOVEMENT =====
        protected float _walkSpeed;
        protected float _runSpeed;
        protected float _currentSpeed;
        protected Vector2? _targetPosition;
        protected List<Vector2>? _path; // Nullable to allow complete clearing
        protected Vector2? _waypoint;
        protected float _stuckTimer;
        protected const float STUCK_THRESHOLD = 0.5f;

        public float CurrentSpeed => _currentSpeed;
        public Vector2? TargetPosition => _targetPosition;
        public List<Vector2>? Path => _path; // Expose path for debug visualization

        // ===== HEALTH =====
        protected float _maxHealth;
        protected float _currentHealth;
        protected bool _isFlashing;
        protected float _flashTimer;
        protected float _flashDuration = 0.5f;
        protected float _flashInterval = 0.1f;
        protected float _flashTime;

        public bool IsFlashing => _isFlashing;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsAlive => _currentHealth > 0f;

        // ===== RENDERING =====
        protected Texture2D? _diamondTexture;
        protected Texture2D? _collisionSphereTexture;
        protected Texture2D? _collisionBufferTexture;
        protected Color _color;
        protected Color _normalColor;
        protected int _size = 32;
        protected int _diamondWidth = 128; // Diamond width in pixels
        protected int _diamondHeight = 64; // Diamond height in pixels

        protected Entity(Vector2 startPosition, Color normalColor, float walkSpeed, float runSpeed, float maxHealth = 100f)
        {
            _position = startPosition;
            _normalColor = normalColor;
            _color = normalColor;
            _walkSpeed = walkSpeed;
            _runSpeed = runSpeed;
            _maxHealth = maxHealth;
            _currentHealth = maxHealth;
            _currentSpeed = 0f;
        }

        /// <summary>
        /// Take damage - reduces health and starts flashing
        /// </summary>
        public virtual void TakeDamage(float damage)
        {
            if (damage <= 0f) return;
            
            _currentHealth = Math.Max(0f, _currentHealth - damage);
            _isFlashing = true;
            _flashTimer = _flashDuration;
            _flashTime = 0f;
        }

        /// <summary>
        /// Take a hit - damage and start flashing (legacy method, calls TakeDamage)
        /// </summary>
        public virtual void TakeHit()
        {
            TakeDamage(10f); // Default damage if not specified
        }

        /// <summary>
        /// Update flash state
        /// </summary>
        protected void UpdateFlashing(float deltaTime)
        {
            if (_isFlashing)
            {
                _flashTimer -= deltaTime;
                _flashTime += deltaTime;
                
                if (_flashTimer <= 0f)
                {
                    _isFlashing = false;
                    _flashTimer = 0f;
                    _flashTime = 0f;
                }
            }
        }

        /// <summary>
        /// Clear movement target
        /// </summary>
        public virtual void ClearTarget()
        {
            // FIX Bug #4: Removed dead and duplicate code
            _targetPosition = null;
            _path = null;
            _waypoint = null;
            _currentSpeed = 0f;
            _stuckTimer = 0f;
        }

        /// <summary>
        /// Initialize all textures for this entity (call during LoadContent, not Draw)
        /// </summary>
        public virtual void InitializeTextures(GraphicsDevice graphicsDevice)
        {
            if (_diamondTexture == null)
            {
                CreateDiamondTexture(graphicsDevice);
            }
            // Collision sphere textures are created on-demand when needed for debug visualization
        }

        /// <summary>
        /// Create diamond texture for isometric rendering
        /// </summary>
        protected void CreateDiamondTexture(GraphicsDevice graphicsDevice)
        {
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
                    float dx = Math.Abs(x - center.X);
                    float dy = Math.Abs(y - center.Y);
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
        /// Create collision sphere texture for debug visualization
        /// Shows both the entity core and the collision buffer zone
        /// </summary>
        protected void CreateCollisionSphereTexture(GraphicsDevice graphicsDevice)
        {
            // Inner circle (entity core)
            int innerRadius = (int)GameConfig.EntityCollisionRadius;
            int innerDiameter = innerRadius * 2;
            
            _collisionSphereTexture = new Texture2D(graphicsDevice, innerDiameter, innerDiameter);
            Color[] innerData = new Color[innerDiameter * innerDiameter];
            
            Vector2 innerCenter = new Vector2(innerRadius, innerRadius);
            
            for (int x = 0; x < innerDiameter; x++)
            {
                for (int y = 0; y < innerDiameter; y++)
                {
                    float dx = x - innerCenter.X;
                    float dy = y - innerCenter.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    
                    // Draw inner circle outline (entity core)
                    if (distance >= innerRadius - 2 && distance <= innerRadius)
                    {
                        innerData[y * innerDiameter + x] = new Color(0, 255, 0, 180); // Green outline
                    }
                    else
                    {
                        innerData[y * innerDiameter + x] = Color.Transparent;
                    }
                }
            }
            
            _collisionSphereTexture.SetData(innerData);
            
            // Outer circle (collision buffer zone)
            float outerRadius = GameConfig.EntityCollisionRadius + GameConfig.CollisionBuffer;
            int outerDiameter = (int)(outerRadius * 2);
            
            _collisionBufferTexture = new Texture2D(graphicsDevice, outerDiameter, outerDiameter);
            Color[] outerData = new Color[outerDiameter * outerDiameter];
            
            Vector2 outerCenter = new Vector2(outerRadius, outerRadius);
            
            for (int x = 0; x < outerDiameter; x++)
            {
                for (int y = 0; y < outerDiameter; y++)
                {
                    float dx = x - outerCenter.X;
                    float dy = y - outerCenter.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    
                    // Draw outer circle outline (collision buffer)
                    if (distance >= outerRadius - 2 && distance <= outerRadius)
                    {
                        outerData[y * outerDiameter + x] = new Color(255, 255, 0, 120); // Yellow outline
                    }
                    // Fill buffer zone with semi-transparent yellow
                    else if (distance > innerRadius && distance < outerRadius)
                    {
                        outerData[y * outerDiameter + x] = new Color(255, 255, 0, 30); // Very transparent yellow
                    }
                    else
                    {
                        outerData[y * outerDiameter + x] = Color.Transparent;
                    }
                }
            }
            
            _collisionBufferTexture.SetData(outerData);
        }
        
        /// <summary>
        /// Draw collision sphere for debug visualization
        /// Shows both the entity core (green) and collision buffer zone (yellow)
        /// </summary>
        public virtual void DrawCollisionSphere(SpriteBatch spriteBatch)
        {
            if (_collisionSphereTexture == null || _collisionBufferTexture == null)
            {
                CreateCollisionSphereTexture(spriteBatch.GraphicsDevice);
            }
            
            // Draw outer collision buffer (yellow)
            if (_collisionBufferTexture != null)
            {
                float outerRadius = GameConfig.EntityCollisionRadius + GameConfig.CollisionBuffer;
                Vector2 outerDrawPos = _position - new Vector2(outerRadius, outerRadius);
                spriteBatch.Draw(_collisionBufferTexture, outerDrawPos, Color.White);
            }
            
            // Draw inner entity core (green)
            if (_collisionSphereTexture != null)
            {
                Vector2 innerDrawPos = _position - new Vector2(GameConfig.EntityCollisionRadius, GameConfig.EntityCollisionRadius);
                spriteBatch.Draw(_collisionSphereTexture, innerDrawPos, Color.White);
            }
        }

        /// <summary>
        /// Draw entity with flash effect
        /// </summary>
        public virtual void Draw(SpriteBatch spriteBatch)
        {
            // Draw bounding box first (behind entity)
            if (_showBoundingBox)
            {
                DrawBoundingBox3D(spriteBatch);
            }
            
            // Texture should be pre-created in InitializeTextures, but fallback for safety
            if (_diamondTexture == null)
            {
                CreateDiamondTexture(spriteBatch.GraphicsDevice);
            }

            // Flash effect
            bool visible = true;
            if (_isFlashing)
            {
                int flashCycle = (int)(_flashTime / _flashInterval);
                visible = (flashCycle % 2 == 0);
            }

            if (visible && _diamondTexture != null)
            {
                // Adjust draw position based on Z height
                Vector2 basePosition = _position;
                if (_zHeight > 0)
                {
                    // In isometric, Z height moves the sprite up on screen
                    basePosition.Y -= _zHeight * 0.5f; // Adjust this multiplier as needed
                }
                
                Vector2 drawPosition = basePosition - new Vector2(_diamondWidth / 2, _diamondHeight / 2);
                spriteBatch.Draw(_diamondTexture, drawPosition, _color);
            }
        }
        
        private static Texture2D? _directionLineTexture;
        
        /// <summary>
        /// Draw direction indicator (arrow) showing which way the entity is facing
        /// </summary>
        public virtual void DrawDirectionIndicator(SpriteBatch spriteBatch, float rotation, float length = 30.0f)
        {
            // Create a simple line texture if needed (shared static texture)
            if (_directionLineTexture == null)
            {
                _directionLineTexture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
                _directionLineTexture.SetData(new[] { Color.White });
            }
            
            // Calculate arrow endpoint
            Vector2 direction = new Vector2((float)Math.Cos(rotation), (float)Math.Sin(rotation));
            Vector2 arrowEnd = _position + direction * length;
            
            // Draw main arrow line
            Vector2 edge = arrowEnd - _position;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float lineLength = edge.Length();
            
            Color arrowColor = new Color(_color.R, _color.G, _color.B, (byte)200); // Slightly transparent
            
            spriteBatch.Draw(
                _directionLineTexture,
                _position,
                null,
                arrowColor,
                angle,
                Vector2.Zero,
                new Vector2(lineLength, 3.0f), // 3 pixel thick line
                SpriteEffects.None,
                0.0f
            );
            
            // Draw arrowhead (small triangle at the end)
            float arrowheadSize = 8.0f;
            
            Vector2 perp = new Vector2(-direction.Y, direction.X);
            
            Vector2 arrowhead1 = arrowEnd + perp * arrowheadSize * 0.5f;
            Vector2 arrowhead2 = arrowEnd - perp * arrowheadSize * 0.5f;
            
            // Draw arrowhead lines
            DrawDirectionLine(spriteBatch, arrowEnd, arrowhead1, arrowColor, _directionLineTexture);
            DrawDirectionLine(spriteBatch, arrowEnd, arrowhead2, arrowColor, _directionLineTexture);
        }
        
        private void DrawDirectionLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, Texture2D texture)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();
            
            spriteBatch.Draw(
                texture,
                start,
                null,
                color,
                angle,
                Vector2.Zero,
                new Vector2(length, 2.0f),
                SpriteEffects.None,
                0.0f
            );
        }

        /// <summary>
        /// Set the sprite texture to use for the bounding box
        /// </summary>
        public void SetBoundingBoxTexture(Texture2D texture)
        {
            _boundingBoxTexture = texture;
        }
        
        /// <summary>
        /// Convert 3D world coordinates (x, y, z) to screen position (pixels)
        /// Z height affects vertical screen position in isometric projection
        /// </summary>
        private Vector2 WorldToScreen3D(float worldX, float worldY, float zHeight, float heightScale = 0.5f)
        {
            float screenX = (worldX - worldY) * (Project9.Shared.IsometricMath.TileWidth / 2.0f);
            float screenY = (worldX + worldY) * (Project9.Shared.IsometricMath.TileHeight / 2.0f) - zHeight * heightScale;
            return new Vector2(screenX, screenY);
        }
        
        /// <summary>
        /// Get the 8 vertices of a 3D isometric bounding box
        /// Returns vertices in order: bottom face (4 vertices) then top face (4 vertices)
        /// </summary>
        public Vector2[] GetBoundingBoxVertices3D()
        {
            float halfWidth = _boundingBoxWidth / 2.0f;
            float halfHeight = _boundingBoxHeight / 2.0f;
            float halfDepth = _boundingBoxDepth / 2.0f;
            
            Vector2[] vertices = new Vector2[8];
            
            // Bottom face vertices (z = 0)
            vertices[0] = WorldToScreen3D(_position.X - halfWidth, _position.Y - halfDepth, 0.0f);
            vertices[1] = WorldToScreen3D(_position.X + halfWidth, _position.Y - halfDepth, 0.0f);
            vertices[2] = WorldToScreen3D(_position.X + halfWidth, _position.Y + halfDepth, 0.0f);
            vertices[3] = WorldToScreen3D(_position.X - halfWidth, _position.Y + halfDepth, 0.0f);
            
            // Top face vertices (z = zHeight)
            vertices[4] = WorldToScreen3D(_position.X - halfWidth, _position.Y - halfDepth, _zHeight);
            vertices[5] = WorldToScreen3D(_position.X + halfWidth, _position.Y - halfDepth, _zHeight);
            vertices[6] = WorldToScreen3D(_position.X + halfWidth, _position.Y + halfDepth, _zHeight);
            vertices[7] = WorldToScreen3D(_position.X - halfWidth, _position.Y + halfDepth, _zHeight);
            
            return vertices;
        }
        
        /// <summary>
        /// Draw the 3D isometric bounding box with sprite texture
        /// </summary>
        public virtual void DrawBoundingBox3D(SpriteBatch spriteBatch)
        {
            if (!_showBoundingBox) return;
            
            Vector2[] vertices = GetBoundingBoxVertices3D();
            
            // If we have a texture, draw it on the faces
            if (_boundingBoxTexture != null)
            {
                DrawBoundingBoxFaces(spriteBatch, vertices);
            }
            else
            {
                // Draw wireframe if no texture
                DrawBoundingBoxWireframe(spriteBatch, vertices);
            }
        }
        
        /// <summary>
        /// Draw bounding box faces with sprite texture
        /// </summary>
        private void DrawBoundingBoxFaces(SpriteBatch spriteBatch, Vector2[] vertices)
        {
            if (_boundingBoxTexture == null) return;
            
            // Draw bottom face
            DrawQuad(spriteBatch, _boundingBoxTexture, 
                vertices[0], vertices[1], vertices[2], vertices[3], 
                new Color(_color.R, _color.G, _color.B, (byte)200));
            
            // Draw top face
            DrawQuad(spriteBatch, _boundingBoxTexture,
                vertices[4], vertices[5], vertices[6], vertices[7],
                new Color(_color.R, _color.G, _color.B, (byte)200));
            
            // Draw front face (facing camera)
            DrawQuad(spriteBatch, _boundingBoxTexture,
                vertices[0], vertices[1], vertices[5], vertices[4],
                new Color(_color.R, _color.G, _color.B, (byte)180));
            
            // Draw back face
            DrawQuad(spriteBatch, _boundingBoxTexture,
                vertices[3], vertices[2], vertices[6], vertices[7],
                new Color(_color.R, _color.G, _color.B, (byte)180));
            
            // Draw left face
            DrawQuad(spriteBatch, _boundingBoxTexture,
                vertices[0], vertices[3], vertices[7], vertices[4],
                new Color(_color.R, _color.G, _color.B, (byte)160));
            
            // Draw right face
            DrawQuad(spriteBatch, _boundingBoxTexture,
                vertices[1], vertices[2], vertices[6], vertices[5],
                new Color(_color.R, _color.G, _color.B, (byte)160));
        }
        
        /// <summary>
        /// Draw bounding box wireframe (for debug visualization)
        /// </summary>
        private void DrawBoundingBoxWireframe(SpriteBatch spriteBatch, Vector2[] vertices)
        {
            Texture2D? lineTexture = GetWhiteTexture(spriteBatch.GraphicsDevice);
            if (lineTexture == null) return;
            
            Color lineColor = new Color(255, 255, 0, 200); // Yellow wireframe
            
            // Bottom face edges
            DrawLine(spriteBatch, lineTexture, vertices[0], vertices[1], lineColor);
            DrawLine(spriteBatch, lineTexture, vertices[1], vertices[2], lineColor);
            DrawLine(spriteBatch, lineTexture, vertices[2], vertices[3], lineColor);
            DrawLine(spriteBatch, lineTexture, vertices[3], vertices[0], lineColor);
            
            // Top face edges
            DrawLine(spriteBatch, lineTexture, vertices[4], vertices[5], lineColor);
            DrawLine(spriteBatch, lineTexture, vertices[5], vertices[6], lineColor);
            DrawLine(spriteBatch, lineTexture, vertices[6], vertices[7], lineColor);
            DrawLine(spriteBatch, lineTexture, vertices[7], vertices[4], lineColor);
            
            // Vertical edges
            DrawLine(spriteBatch, lineTexture, vertices[0], vertices[4], lineColor);
            DrawLine(spriteBatch, lineTexture, vertices[1], vertices[5], lineColor);
            DrawLine(spriteBatch, lineTexture, vertices[2], vertices[6], lineColor);
            DrawLine(spriteBatch, lineTexture, vertices[3], vertices[7], lineColor);
        }
        
        /// <summary>
        /// Draw a quad (4-sided polygon) with texture
        /// </summary>
        private void DrawQuad(SpriteBatch spriteBatch, Texture2D texture, 
            Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3, Color color)
        {
            // Calculate quad bounds
            float minX = Math.Min(Math.Min(v0.X, v1.X), Math.Min(v2.X, v3.X));
            float maxX = Math.Max(Math.Max(v0.X, v1.X), Math.Max(v2.X, v3.X));
            float minY = Math.Min(Math.Min(v0.Y, v1.Y), Math.Min(v2.Y, v3.Y));
            float maxY = Math.Max(Math.Max(v0.Y, v1.Y), Math.Max(v2.Y, v3.Y));
            
            float width = maxX - minX;
            float height = maxY - minY;
            
            if (width <= 0 || height <= 0) return;
            
            // Draw the texture stretched to fit the quad bounds
            // Note: This is a simplified approach. For proper perspective, you'd need custom shaders
            spriteBatch.Draw(texture, 
                new Rectangle((int)minX, (int)minY, (int)width, (int)height),
                null, color, 0f, Vector2.Zero, SpriteEffects.None, 0f);
        }
        
        /// <summary>
        /// Draw a line between two points
        /// </summary>
        private void DrawLine(SpriteBatch spriteBatch, Texture2D texture, Vector2 start, Vector2 end, Color color)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();
            
            if (length <= 0) return;
            
            spriteBatch.Draw(
                texture,
                start,
                null,
                color,
                angle,
                Vector2.Zero,
                new Vector2(length, 2.0f),
                SpriteEffects.None,
                0.0f
            );
        }
        
        private static Texture2D? _whiteTexture;
        
        /// <summary>
        /// Get or create a white 1x1 texture for drawing
        /// </summary>
        private Texture2D? GetWhiteTexture(GraphicsDevice graphicsDevice)
        {
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(graphicsDevice, 1, 1);
                _whiteTexture.SetData(new[] { Color.White });
            }
            return _whiteTexture;
        }

        /// <summary>
        /// Update entity - must be implemented by derived classes
        /// </summary>
        public abstract void Update(float deltaTime);
    }
}

