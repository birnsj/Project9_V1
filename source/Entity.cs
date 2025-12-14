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
            if (_boundingBoxTexture == null)
            {
                CreateBoundingBoxTexture(graphicsDevice);
            }
            // Collision sphere textures are created on-demand when needed for debug visualization
        }
        
        /// <summary>
        /// Create a simple white texture for bounding box faces
        /// </summary>
        protected void CreateBoundingBoxTexture(GraphicsDevice graphicsDevice)
        {
            // Create a small white texture (1x1 pixel, will be stretched)
            _boundingBoxTexture = new Texture2D(graphicsDevice, 1, 1);
            _boundingBoxTexture.SetData(new Color[] { Color.White });
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
                // Use lower layerDepth (0.1) so entity sprite draws behind bounding box
                spriteBatch.Draw(_diamondTexture, drawPosition, null, _color, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0.1f);
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
        /// Get the 8 vertices of a 3D isometric bounding box in world coordinates
        /// Returns vertices in order: bottom face (4 vertices) then top face (4 vertices)
        /// The SpriteBatch transform will handle camera positioning, we just need world coordinates
        /// </summary>
        public Vector2[] GetBoundingBoxVertices3D()
        {
            float halfWidth = _boundingBoxWidth / 2.0f;
            float halfHeight = _boundingBoxHeight / 2.0f;
            
            // For isometric bounding box, we use diamond shape like the entity
            // Bottom face: isometric diamond at z=0
            // Top face: isometric diamond at z=zHeight, offset upward in world Y
            const float heightScale = 0.5f;
            float zOffsetY = _zHeight * heightScale;
            
            Vector2[] vertices = new Vector2[8];
            
            // Bottom face vertices (z = 0) - isometric diamond
            vertices[0] = new Vector2(_position.X, _position.Y - halfHeight); // Top
            vertices[1] = new Vector2(_position.X + halfWidth, _position.Y); // Right
            vertices[2] = new Vector2(_position.X, _position.Y + halfHeight); // Bottom
            vertices[3] = new Vector2(_position.X - halfWidth, _position.Y); // Left
            
            // Top face vertices (z = zHeight) - same diamond shape, offset upward by Z height
            vertices[4] = new Vector2(_position.X, _position.Y - halfHeight - zOffsetY); // Top
            vertices[5] = new Vector2(_position.X + halfWidth, _position.Y - zOffsetY); // Right
            vertices[6] = new Vector2(_position.X, _position.Y + halfHeight - zOffsetY); // Bottom
            vertices[7] = new Vector2(_position.X - halfWidth, _position.Y - zOffsetY); // Left
            
            return vertices;
        }
        
        /// <summary>
        /// Draw the 3D isometric bounding box using the same approach as the editor
        /// Uses filled polygons with 30% opacity cyan, matching editor rendering
        /// </summary>
        public virtual void DrawBoundingBox3D(SpriteBatch spriteBatch)
        {
            if (!_showBoundingBox) return;
            
            float halfWidth = _boundingBoxWidth / 2.0f;
            float halfHeight = _boundingBoxHeight / 2.0f;
            
            // Cyan color for wireframe
            Color cyanColor = new Color((byte)0, (byte)255, (byte)255, (byte)255);
            
            // ZHeight represents the TOP of the object
            // Base is always at z = 0, top is at z = zHeight
            // We draw in world coordinates (SpriteBatch has camera transform applied)
            
            // If zHeight is 0 or less, just draw the base diamond
            if (_zHeight <= 0)
            {
                // Draw a simple diamond outline at the base
                Vector2[] diamondPoints = new Vector2[]
                {
                    new Vector2(_position.X, _position.Y - halfHeight),
                    new Vector2(_position.X + halfWidth, _position.Y),
                    new Vector2(_position.X, _position.Y + halfHeight),
                    new Vector2(_position.X - halfWidth, _position.Y)
                };
                
                // Draw outline only
                DrawPolygonOutline(spriteBatch, diamondPoints, cyanColor, 3.0f);
                return;
            }
            
            // For 3D bounding box, we use the same isometric diamond shape as the entity
            // For isometric Z projection, we adjust the Y coordinate based on Z height
            const float heightScale = 0.5f;
            float zOffsetY = _zHeight * heightScale;
            
            // Bottom face corners (z = 0) - base of the object (isometric diamond)
            Vector2 bottomTop = new Vector2(_position.X, _position.Y - halfHeight);
            Vector2 bottomRight = new Vector2(_position.X + halfWidth, _position.Y);
            Vector2 bottomBottom = new Vector2(_position.X, _position.Y + halfHeight);
            Vector2 bottomLeft = new Vector2(_position.X - halfWidth, _position.Y);
            
            // Top face corners (z = zHeight) - top of the object
            // In isometric, Z height affects the Y coordinate (moves up in screen space)
            Vector2 topTop = new Vector2(_position.X, _position.Y - halfHeight - zOffsetY);
            Vector2 topRight = new Vector2(_position.X + halfWidth, _position.Y - zOffsetY);
            Vector2 topBottom = new Vector2(_position.X, _position.Y + halfHeight - zOffsetY);
            Vector2 topLeft = new Vector2(_position.X - halfWidth, _position.Y - zOffsetY);
            
            // Draw wireframe outline using cyan color
            Texture2D? lineTexture = GetWhiteTexture(spriteBatch.GraphicsDevice);
            if (lineTexture == null) return;
            
            // Bottom face (isometric diamond at z=0) - draw first so it's behind
            DrawLine(spriteBatch, lineTexture, bottomTop, bottomRight, cyanColor, 3.0f);
            DrawLine(spriteBatch, lineTexture, bottomRight, bottomBottom, cyanColor, 3.0f);
            DrawLine(spriteBatch, lineTexture, bottomBottom, bottomLeft, cyanColor, 3.0f);
            DrawLine(spriteBatch, lineTexture, bottomLeft, bottomTop, cyanColor, 3.0f);
            
            // Vertical edges connecting bottom to top - draw these before top face so they're visible
            DrawLine(spriteBatch, lineTexture, bottomTop, topTop, cyanColor, 3.0f);
            DrawLine(spriteBatch, lineTexture, bottomRight, topRight, cyanColor, 3.0f);
            DrawLine(spriteBatch, lineTexture, bottomBottom, topBottom, cyanColor, 3.0f);
            DrawLine(spriteBatch, lineTexture, bottomLeft, topLeft, cyanColor, 3.0f);
            
            // Top face (isometric diamond at z=zHeight) - draw last so it's on top
            DrawLine(spriteBatch, lineTexture, topTop, topRight, cyanColor, 3.0f);
            DrawLine(spriteBatch, lineTexture, topRight, topBottom, cyanColor, 3.0f);
            DrawLine(spriteBatch, lineTexture, topBottom, topLeft, cyanColor, 3.0f);
            DrawLine(spriteBatch, lineTexture, topLeft, topTop, cyanColor, 3.0f);
        }
        
        /// <summary>
        /// Draw a filled polygon using SpriteBatch (matches editor's FillPolygon)
        /// Uses a simple approach: draw multiple overlapping lines to approximate filled polygon
        /// For better quality, would need custom shader or texture generation
        /// </summary>
        private void DrawFilledPolygon(SpriteBatch spriteBatch, Vector2[] vertices, Color color)
        {
            if (vertices.Length < 3) return;
            
            Texture2D? whiteTexture = GetWhiteTexture(spriteBatch.GraphicsDevice);
            if (whiteTexture == null) return;
            
            // For a simple filled polygon approximation, draw lines between all vertices
            // This creates a filled appearance for convex polygons
            // For quads (4 vertices), draw two triangles
            if (vertices.Length == 4)
            {
                // Draw as two triangles: [0,1,2] and [0,2,3]
                DrawFilledTriangle(spriteBatch, whiteTexture, vertices[0], vertices[1], vertices[2], color);
                DrawFilledTriangle(spriteBatch, whiteTexture, vertices[0], vertices[2], vertices[3], color);
            }
            else
            {
                // For other polygons, triangulate by fanning from first vertex
                for (int i = 1; i < vertices.Length - 1; i++)
                {
                    DrawFilledTriangle(spriteBatch, whiteTexture, vertices[0], vertices[i], vertices[i + 1], color);
                }
            }
        }
        
        /// <summary>
        /// Draw a filled triangle by drawing many overlapping horizontal lines
        /// This creates a solid fill appearance
        /// </summary>
        private void DrawFilledTriangle(SpriteBatch spriteBatch, Texture2D texture, Vector2 v0, Vector2 v1, Vector2 v2, Color color)
        {
            // Sort vertices by Y
            Vector2[] sorted = new Vector2[] { v0, v1, v2 };
            Array.Sort(sorted, (a, b) => a.Y.CompareTo(b.Y));
            
            Vector2 top = sorted[0];
            Vector2 mid = sorted[1];
            Vector2 bot = sorted[2];
            
            if (Math.Abs(bot.Y - top.Y) < 0.1f) return;
            
            // Draw top half (top to mid)
            if (Math.Abs(mid.Y - top.Y) > 0.1f)
            {
                int topY = (int)Math.Ceiling(top.Y);
                int midY = (int)Math.Floor(mid.Y);
                for (int y = topY; y <= midY; y++)
                {
                    float t = (y - top.Y) / (mid.Y - top.Y);
                    float x1 = top.X + (mid.X - top.X) * t;
                    float x2 = top.X + (bot.X - top.X) * ((y - top.Y) / (bot.Y - top.Y));
                    if (x1 > x2) { float temp = x1; x1 = x2; x2 = temp; }
                    if (x2 - x1 > 0.1f)
                        DrawLine(spriteBatch, texture, new Vector2(x1, y), new Vector2(x2, y), color, 2.0f);
                }
            }
            
            // Draw bottom half (mid to bot)
            if (Math.Abs(bot.Y - mid.Y) > 0.1f)
            {
                int midY = (int)Math.Ceiling(mid.Y);
                int botY = (int)Math.Floor(bot.Y);
                for (int y = midY; y <= botY; y++)
                {
                    float t = (y - mid.Y) / (bot.Y - mid.Y);
                    float x1 = mid.X + (bot.X - mid.X) * t;
                    float x2 = top.X + (bot.X - top.X) * ((y - top.Y) / (bot.Y - top.Y));
                    if (x1 > x2) { float temp = x1; x1 = x2; x2 = temp; }
                    if (x2 - x1 > 0.1f)
                        DrawLine(spriteBatch, texture, new Vector2(x1, y), new Vector2(x2, y), color, 2.0f);
                }
            }
        }
        
        /// <summary>
        /// Draw a polygon outline using SpriteBatch (matches editor's DrawPolygon)
        /// </summary>
        private void DrawPolygonOutline(SpriteBatch spriteBatch, Vector2[] vertices, Color color, float thickness)
        {
            if (vertices.Length < 2) return;
            
            Texture2D? lineTexture = GetWhiteTexture(spriteBatch.GraphicsDevice);
            if (lineTexture == null) return;
            
            // Draw lines between consecutive vertices, and close the polygon
            for (int i = 0; i < vertices.Length; i++)
            {
                int next = (i + 1) % vertices.Length;
                DrawLine(spriteBatch, lineTexture, vertices[i], vertices[next], color, thickness);
            }
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
        private void DrawLine(SpriteBatch spriteBatch, Texture2D texture, Vector2 start, Vector2 end, Color color, float thickness = 3.0f)
        {
            Vector2 edge = end - start;
            float length = edge.Length();
            
            if (length <= 0.1f) return;
            
            // Check if line is mostly vertical (Y component is much larger than X component)
            // This handles both perfectly vertical lines and near-vertical lines in isometric projection
            bool isMostlyVertical = Math.Abs(edge.Y) > Math.Abs(edge.X) * 10.0f && Math.Abs(edge.Y) > 0.1f;
            
            // Use higher layerDepth (0.99) so bounding box draws on top of entity sprites and tiles
            const float boundingBoxLayerDepth = 0.99f;
            
            if (isMostlyVertical)
            {
                // Mostly vertical line - draw as a vertical rectangle for better visibility
                float halfThickness = thickness / 2.0f;
                float minY = Math.Min(start.Y, end.Y);
                float maxY = Math.Max(start.Y, end.Y);
                float centerX = (start.X + end.X) / 2.0f;
                
                Rectangle destRect = new Rectangle(
                    (int)(centerX - halfThickness),
                    (int)minY,
                    (int)thickness,
                    (int)(maxY - minY)
                );
                spriteBatch.Draw(texture, destRect, null, color, 0f, Vector2.Zero, SpriteEffects.None, boundingBoxLayerDepth);
            }
            else
            {
                // Non-vertical line - use rotated texture approach
                float angle = (float)Math.Atan2(edge.Y, edge.X);
                spriteBatch.Draw(
                    texture,
                    start,
                    null,
                    color,
                    angle,
                    new Vector2(0, 0.5f),
                    new Vector2(length, thickness),
                    SpriteEffects.None,
                    boundingBoxLayerDepth
                );
            }
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


