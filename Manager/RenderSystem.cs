using System;
using System.Collections.Generic;
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
        private ViewportCamera _camera;
        private SpriteFont? _uiFont;
        
        private bool _showGrid64x32 = true;
        private bool _showCollision = true;
        private bool _showCollisionSpheres = true; // Show collision spheres for entities
        private bool _showPath = false; // Show path debug visualization
        private bool _showBoundingBoxes = true; // Show bounding boxes for entities (default on)
        private Texture2D? _gridLineTexture;
        private Texture2D? _collisionDiamondTexture;
        private Texture2D? _clickEffectTexture;
        private Texture2D? _bloodSplatTexture;
        private Texture2D? _pistolRangeCircleTexture;
        private Texture2D? _whiteTexture; // Reusable white texture for various drawing operations
        
        // Click effect
        private Vector2? _clickEffectPosition;
        private float _clickEffectTimer = 0.0f;
        
        // Renderer instances
        private HealthBarRenderer _healthBarRenderer;
        private PathRenderer _pathRenderer;
        private DamageNumberRenderer _damageNumberRenderer;
        private UIRenderer _uiRenderer;
        
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
        
        public bool ShowPath
        {
            get => _showPath;
            set => _showPath = value;
        }
        
        public bool ShowBoundingBoxes
        {
            get => _showBoundingBoxes;
            set => _showBoundingBoxes = value;
        }
        
        public int LastDrawCallCount => _lastDrawCallCount;

        public RenderSystem(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, IsometricMap map, ViewportCamera camera, SpriteFont? uiFont)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _map = map;
            _camera = camera;
            _uiFont = uiFont;
            
            // Initialize renderers
            _healthBarRenderer = new HealthBarRenderer(graphicsDevice, uiFont);
            _pathRenderer = new PathRenderer(graphicsDevice);
            _damageNumberRenderer = new DamageNumberRenderer(uiFont);
            _uiRenderer = new UIRenderer(graphicsDevice, uiFont);
        }

        /// <summary>
        /// Show click effect at position
        /// </summary>
        public void ShowClickEffect(Vector2 worldPosition)
        {
            _clickEffectPosition = worldPosition;
            _clickEffectTimer = GameConfig.ClickEffectDuration;
        }

        /// <summary>
        /// Update click effect
        /// </summary>
        public void UpdateClickEffect(float deltaTime)
        {
            if (_clickEffectTimer > 0.0f)
            {
                _clickEffectTimer -= deltaTime;
                if (_clickEffectTimer <= 0.0f)
                {
                    _clickEffectPosition = null;
                    _clickEffectTimer = 0.0f;
                }
            }
        }
        
        /// <summary>
        /// Add a damage number to display
        /// </summary>
        public void ShowDamageNumber(Vector2 worldPosition, float damage)
        {
            _damageNumberRenderer.ShowDamageNumber(worldPosition, damage);
        }
        
        /// <summary>
        /// Update all damage numbers
        /// </summary>
        public void UpdateDamageNumbers(float deltaTime)
        {
            _damageNumberRenderer.UpdateDamageNumbers(deltaTime);
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

            // Use frustum-culled version for better performance
            _map.Draw(_spriteBatch, _camera, _graphicsDevice);
            _lastDrawCallCount++;

            // Draw click effect
            if (_clickEffectPosition.HasValue && _clickEffectTimer > 0.0f)
            {
                DrawClickEffect(_spriteBatch);
                _lastDrawCallCount++;
            }

            if (_showGrid64x32)
            {
                DrawGrid64x32(_spriteBatch);
                _lastDrawCallCount++;
            }

            // Calculate visible bounds for frustum culling
            Vector2 screenTopLeft = ScreenToWorld(Vector2.Zero);
            Vector2 screenBottomRight = ScreenToWorld(new Vector2(
                _graphicsDevice.Viewport.Width,
                _graphicsDevice.Viewport.Height
            ));
            
            // Expand bounds with margin for safety (account for entity size and zoom)
            float margin = GameConfig.FrustumCullingMargin / _camera.Zoom; // Larger margin when zoomed out
            float minX = screenTopLeft.X - margin;
            float maxX = screenBottomRight.X + margin;
            float minY = screenTopLeft.Y - margin;
            float maxY = screenBottomRight.Y + margin;

            // Collect all entities for isometric depth sorting
            var entitiesToDraw = new List<(Entity? entity, float depth, string type)>();
            var worldObjectsToDraw = new List<(WorldObject worldObject, float depth)>();
            
            // Collect bounding box faces for per-face sorting (only when bounding boxes are shown)
            var boundingBoxFaces = new List<(Entity? entity, Vector2[] vertices, float depth, Color color)>();
            
            // Height scale for isometric Z projection (matches rendering)
            const float heightScale = 0.5f;
            
            // Add cameras
            foreach (var camera in entityManager.Cameras)
            {
                if (camera.Position.X + 100 >= minX && camera.Position.X - 100 <= maxX &&
                    camera.Position.Y + 100 >= minY && camera.Position.Y - 100 <= maxY)
                {
                    // Sort using isometric depth formula: depth = (X + Y) - zHeight * scale
                    float zOffsetY = camera.ZHeight * heightScale;
                    float topFaceCenterY = camera.Position.Y - zOffsetY;
                    float depth = (camera.Position.X + topFaceCenterY) - (camera.ZHeight * 0.3f);
                    entitiesToDraw.Add((camera, depth, "camera"));
                    
                    // If bounding boxes are shown, extract faces for per-face sorting
                    if (_showBoundingBoxes && camera.ZHeight > 0)
                    {
                        AddBoundingBoxFaces(camera, boundingBoxFaces, heightScale);
                    }
                }
            }
            
            // Add enemies
            foreach (var enemy in entityManager.Enemies)
            {
                if (enemy.Position.X + 50 >= minX && enemy.Position.X - 50 <= maxX &&
                    enemy.Position.Y + 50 >= minY && enemy.Position.Y - 50 <= maxY)
                {
                    // Sort using isometric depth formula: depth = (X + Y) - zHeight * scale
                    // In isometric projection, screen Y = (X + Y) * scale, so objects with higher (X+Y) are more forward
                    // Z height moves objects up in screen space, so subtract it from depth
                    // Use the top face center for stable sorting of tall objects
                    float halfHeight = enemy.BoundingBoxHeight / 2.0f;
                    float zOffsetY = enemy.ZHeight * heightScale;
                    float topFaceCenterY = enemy.Position.Y - zOffsetY;
                    // Isometric depth: (X + Y) represents forwardness, subtract Z offset
                    float depth = (enemy.Position.X + topFaceCenterY) - (enemy.ZHeight * 0.3f);
                    entitiesToDraw.Add((enemy, depth, "enemy"));
                    
                    // If bounding boxes are shown, extract faces for per-face sorting
                    if (_showBoundingBoxes && enemy.ZHeight > 0)
                    {
                        AddBoundingBoxFaces(enemy, boundingBoxFaces, heightScale);
                    }
                }
            }
            
            // Add weapon pickups
            foreach (var weaponPickup in entityManager.WeaponPickups)
            {
                if (!weaponPickup.IsPickedUp &&
                    weaponPickup.Position.X + 30 >= minX && weaponPickup.Position.X - 30 <= maxX &&
                    weaponPickup.Position.Y + 30 >= minY && weaponPickup.Position.Y - 30 <= maxY)
                {
                    // Sort using isometric depth formula: depth = (X + Y) - zHeight * scale
                    float zOffsetY = weaponPickup.ZHeight * heightScale;
                    float topFaceCenterY = weaponPickup.Position.Y - zOffsetY;
                    float depth = (weaponPickup.Position.X + topFaceCenterY) - (weaponPickup.ZHeight * 0.3f);
                    entitiesToDraw.Add((weaponPickup, depth, "weapon"));
                    
                    // If bounding boxes are shown, extract faces for per-face sorting
                    if (_showBoundingBoxes && weaponPickup.ZHeight > 0)
                    {
                        AddBoundingBoxFaces(weaponPickup, boundingBoxFaces, heightScale);
                    }
                }
            }
            
            // Add player
            // Sort using isometric depth formula: depth = (X + Y) - zHeight * scale
            float playerZOffsetY = entityManager.Player.ZHeight * heightScale;
            float playerTopFaceCenterY = entityManager.Player.Position.Y - playerZOffsetY;
            float playerDepth = (entityManager.Player.Position.X + playerTopFaceCenterY) - (entityManager.Player.ZHeight * 0.3f);
            entitiesToDraw.Add((entityManager.Player, playerDepth, "player"));
            
            // If bounding boxes are shown, extract faces for per-face sorting
            if (_showBoundingBoxes && entityManager.Player.ZHeight > 0)
            {
                AddBoundingBoxFaces(entityManager.Player, boundingBoxFaces, heightScale);
            }
            
            // Add world objects (furniture)
            foreach (var worldObject in _map.WorldObjects)
            {
                // Frustum culling for world objects
                float objHalfWidth = worldObject.DiamondWidth / 2.0f;
                float objHalfHeight = worldObject.DiamondHeight / 2.0f;
                if (worldObject.Position.X + objHalfWidth >= minX && worldObject.Position.X - objHalfWidth <= maxX &&
                    worldObject.Position.Y + objHalfHeight >= minY && worldObject.Position.Y - objHalfHeight <= maxY)
                {
                    // Sort using isometric depth formula: depth = (X + Y) - zHeight * scale
                    float zOffsetY = worldObject.ZHeight * heightScale;
                    float topFaceCenterY = worldObject.Position.Y - zOffsetY;
                    float depth = (worldObject.Position.X + topFaceCenterY) - (worldObject.ZHeight * 0.3f);
                    worldObjectsToDraw.Add((worldObject, depth));
                    
                    // If bounding boxes are shown, extract faces for per-face sorting
                    if (_showBoundingBoxes && worldObject.ZHeight > 0)
                    {
                        AddWorldObjectBoundingBoxFaces(worldObject, boundingBoxFaces, heightScale);
                    }
                }
            }
            
            // Sort bounding box faces by depth (back to front)
            boundingBoxFaces.Sort((a, b) => a.depth.CompareTo(b.depth));
            
            // Sort by isometric depth (back to front)
            entitiesToDraw.Sort((a, b) => a.depth.CompareTo(b.depth));
            worldObjectsToDraw.Sort((a, b) => a.depth.CompareTo(b.depth));
            
            // Merge sorted lists for proper depth sorting
            var allToDraw = new List<(object drawable, float depth, string type)>();
            foreach (var (entity, depth, type) in entitiesToDraw)
            {
                allToDraw.Add((entity!, depth, type));
            }
            foreach (var (worldObject, depth) in worldObjectsToDraw)
            {
                allToDraw.Add((worldObject, depth, "worldobject"));
            }
            allToDraw.Sort((a, b) => a.depth.CompareTo(b.depth));
            
            // Create line texture for world object wireframes if needed
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(_graphicsDevice, 1, 1);
                _whiteTexture.SetData(new[] { Color.White });
            }
            
            // Draw all objects in sorted order first (sprites behind bounding boxes)
            foreach (var (drawable, depth, type) in allToDraw)
            {
                if (type == "worldobject")
                {
                    var worldObject = drawable as WorldObject;
                    if (worldObject != null)
                    {
                        worldObject.ShowBoundingBox = _showBoundingBoxes;
                        worldObject.Draw(_spriteBatch, _whiteTexture);
                        _lastDrawCallCount++;
                    }
                    continue;
                }
                
                var entity = drawable as Entity;
                if (entity == null)
                    continue;
                    
                // Set bounding box visibility to false since we're drawing faces separately
                entity.ShowBoundingBox = false;
                
                if (type == "camera")
                {
                    var camera = entity as SecurityCamera;
                    if (camera != null)
                    {
                        // Only draw sight cone if camera is alive
                        if (camera.IsAlive)
                        {
                            camera.DrawSightCone(_spriteBatch);
                        }
                        camera.Draw(_spriteBatch);
                        
                        // Draw health bar if camera has been damaged (health < maxHealth)
                        if (camera.IsAlive && camera.CurrentHealth < camera.MaxHealth)
                        {
                            // Cast camera to Enemy for health bar rendering (cameras inherit from Enemy)
                            if (camera is Enemy cameraEnemy)
                            {
                                _healthBarRenderer.DrawEnemyHealthBar(_spriteBatch, cameraEnemy);
                            }
                        }
                        
                        _lastDrawCallCount += 2; // Sight cone, sprite
                    }
                }
                else if (type == "enemy")
                {
                    var enemy = entity as Enemy;
                    if (enemy != null)
                    {
                        // Draw blood splat under dead enemies first (so it appears below)
                        if (enemy.IsDead)
                        {
                            DrawBloodSplat(_spriteBatch, enemy.Position);
                        }
                        
                        // Skip drawing aggro/sight cone for dead enemies
                        if (!enemy.IsAlive && !enemy.IsDead)
                            continue;
                        
                        if (!enemy.IsDead)
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
                            
                            // Draw direction indicator
                            enemy.DrawDirectionIndicator(_spriteBatch, enemy.Rotation);
                        }
                        
                        // Always draw enemy sprite (alive or dead with pulse)
                        enemy.Draw(_spriteBatch);
                        
                        // Draw health bar above enemy (only if alive and player is within detection range)
                        if (enemy.IsAlive)
                        {
                            float distanceSquared = Vector2.DistanceSquared(entityManager.Player.Position, enemy.Position);
                            float effectiveDetectionRange = enemy.HasDetectedPlayer 
                                ? enemy.DetectionRange 
                                : (entityManager.Player.IsSneaking ? enemy.DetectionRange * GameConfig.EnemySneakDetectionMultiplier : enemy.DetectionRange);
                            float effectiveRangeSquared = effectiveDetectionRange * effectiveDetectionRange;
                            
                            // Only show health bar if player is within detection range
                            if (distanceSquared <= effectiveRangeSquared)
                            {
                                _healthBarRenderer.DrawEnemyHealthBar(_spriteBatch, enemy);
                            }
                        }
                        
                        int drawCount = enemy.IsDead ? 2 : (_showCollisionSpheres ? 6 : 5); // Blood splat + sprite for dead, full set for alive
                        _lastDrawCallCount += drawCount;
                    }
                }
                else if (type == "weapon")
                {
                    var weaponPickup = entity as WeaponPickup;
                    if (weaponPickup != null)
                    {
                        weaponPickup.Draw(_spriteBatch);
                        _lastDrawCallCount++;
                    }
                }
                else if (type == "player")
                {
                    var player = entity as Player;
                    if (player != null)
                    {
                        // Draw blood splat under dead player first (so it appears below)
                        if (player.IsDead)
                        {
                            DrawBloodSplat(_spriteBatch, player.Position);
                        }
                        
                        if (!player.IsDead && _showCollisionSpheres)
                        {
                            player.DrawCollisionSphere(_spriteBatch);
                        }
                        player.Draw(_spriteBatch);
                        
                        // Draw direction indicator (only if alive)
                        if (!player.IsDead)
                        {
                            player.DrawDirectionIndicator(_spriteBatch, player.Rotation);
                        }
                        
                        // Draw equipped weapon after direction indicator so it's on top (only if alive and has weapon)
                        if (!player.IsDead && player.EquippedWeapon != null)
                        {
                            DrawPlayerWeapon(_spriteBatch, player);
                            
                            // Draw range indicator for gun
                            if (player.EquippedWeapon is Gun)
                            {
                                DrawPistolRange(_spriteBatch, player);
                            }
                        }
                        
                        int playerDrawCount = player.IsDead ? 2 : (_showCollisionSpheres ? 3 : 2); // Blood splat + sprite for dead, full set for alive
                        _lastDrawCallCount += playerDrawCount;
                    }
                }
            }
            
            // Draw projectiles (always on top, no depth sorting needed)
            foreach (var projectile in entityManager.Projectiles)
            {
                if (!projectile.IsExpired)
                {
                    // Frustum culling for projectiles
                    if (projectile.Position.X + 10 >= minX && projectile.Position.X - 10 <= maxX &&
                        projectile.Position.Y + 10 >= minY && projectile.Position.Y - 10 <= maxY)
                    {
                        projectile.Draw(_spriteBatch);
                        _lastDrawCallCount++;
                    }
                }
            }
            
            // Draw debug path for player (only if not dragging/following cursor and path debug is enabled)
            if (_showPath && !entityManager.IsFollowingCursor)
            {
                _pathRenderer.DrawDebugPath(_spriteBatch, entityManager.Player);
            }

            // Draw collision cells
            if (_showCollision)
            {
                DrawCollisionCells(_spriteBatch, collisionManager);
                _lastDrawCallCount++;
            }

            // Draw damage numbers in world space (before ending world space rendering)
            _damageNumberRenderer.DrawDamageNumbers(_spriteBatch);

            // Draw name tags when hovering over entities (only if not in combat)
            if (!entityManager.IsPlayerInCombat())
            {
                DrawNameTags(_spriteBatch, entityManager);
            }

            _spriteBatch.End();
            
            // Draw bounding box faces last (after everything else) if enabled
            if (_showBoundingBoxes && boundingBoxFaces.Count > 0)
            {
                _spriteBatch.Begin(
                    transformMatrix: _camera.GetTransform(),
                    samplerState: SamplerState.PointClamp
                );
                
                Texture2D? lineTexture = _whiteTexture;
                foreach (var (entity, vertices, depth, color) in boundingBoxFaces)
                {
                    DrawBoundingBoxFace(_spriteBatch, lineTexture, vertices, color);
                }
                
                _spriteBatch.End();
            }

            // Screen space rendering (UI)
            if (_uiFont != null)
            {
                _spriteBatch.Begin();

                // Health bar in lower left corner (only if alive)
                if (entityManager.Player.IsAlive)
                {
                    _healthBarRenderer.DrawPlayerHealthBar(_spriteBatch, entityManager.Player);
                }

                // Death screen (if player is dead)
                if (entityManager.Player.IsDead)
                {
                    _uiRenderer.DrawDeathScreen(_spriteBatch, entityManager.Player);
                }

                // Version number
                _uiRenderer.DrawVersion(_spriteBatch);

                // Sneak indicator
                _uiRenderer.DrawSneakIndicator(_spriteBatch, entityManager.Player.IsSneaking, entityManager.Player.IsAlive);
                
                // Alarm countdown
                _uiRenderer.DrawAlarmCountdown(_spriteBatch, entityManager.AlarmActive, entityManager.AlarmTimer);

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
        
        /// <summary>
        /// Calculate isometric depth for sorting entities (back to front)
        /// Higher depth value = further back, should be drawn first
        /// Uses Y position (higher Y = further back) plus X position for tie-breaking
        /// Z height is subtracted so higher objects appear in front
        /// </summary>
        private float CalculateIsometricDepth(Vector2 position, float zHeight)
        {
            // In isometric view, objects further back have higher Y values
            // We also consider X position for tie-breaking (objects to the right appear slightly in front)
            // Z height is subtracted so objects at higher elevations appear in front
            return position.Y + position.X * 0.001f - zHeight * 0.5f;
        }
        
        /// <summary>
        /// Calculate isometric depth using separate X and Y coordinates
        /// </summary>
        private float CalculateIsometricDepth(float x, float y, float zHeight)
        {
            return y + x * 0.001f - zHeight * 0.5f;
        }

        
        private void DrawClickEffect(SpriteBatch spriteBatch)
        {
            if (!_clickEffectPosition.HasValue)
                return;

            // Create texture if needed
            if (_clickEffectTexture == null)
            {
                int size = 64;
                _clickEffectTexture = new Texture2D(_graphicsDevice, size, size);
                Color[] colorData = new Color[size * size];
                
                Vector2 center = new Vector2(size / 2.0f, size / 2.0f);
                float radius = size / 2.0f;
                
                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        float normalizedDist = distance / radius;
                        
                        // Outer ring (pulsing effect)
                        if (normalizedDist >= 0.7f && normalizedDist <= 1.0f)
                        {
                            float ringAlpha = 1.0f - (normalizedDist - 0.7f) / 0.3f;
                            colorData[y * size + x] = new Color((byte)100, (byte)200, (byte)255, (byte)(ringAlpha * 200));
                        }
                        // Inner circle
                        else if (normalizedDist <= 0.3f)
                        {
                            float innerAlpha = 1.0f - (normalizedDist / 0.3f);
                            colorData[y * size + x] = new Color((byte)150, (byte)220, (byte)255, (byte)(innerAlpha * 150));
                        }
                        else
                        {
                            colorData[y * size + x] = Color.Transparent;
                        }
                    }
                }
                
                _clickEffectTexture.SetData(colorData);
            }

            // Calculate fade based on timer
            float fadeProgress = _clickEffectTimer / GameConfig.ClickEffectDuration;
            float pulseScale = 1.0f + (1.0f - fadeProgress) * 0.5f; // Grows from 1.0 to 1.5
            byte fadeAlpha = (byte)(fadeProgress * 255);
            
            Vector2 drawPos = _clickEffectPosition.Value - new Vector2(_clickEffectTexture.Width / 2.0f * pulseScale, _clickEffectTexture.Height / 2.0f * pulseScale);
            Color tint = new Color((byte)255, (byte)255, (byte)255, fadeAlpha);
            
            spriteBatch.Draw(
                _clickEffectTexture,
                drawPos,
                null,
                tint,
                0f,
                Vector2.Zero,
                pulseScale,
                SpriteEffects.None,
                0f
            );
        }


        /// <summary>
        /// Draw name tags above entities when mouse is hovering over them (only when not in combat)
        /// </summary>
        private void DrawNameTags(SpriteBatch spriteBatch, EntityManager entityManager)
        {
            if (_uiFont == null)
                return;

            // Get mouse position in world coordinates
            Microsoft.Xna.Framework.Input.MouseState mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            Vector2 mouseScreenPos = new Vector2(mouseState.X, mouseState.Y);
            Vector2 mouseWorldPos = ScreenToWorld(mouseScreenPos);

            const float hoverRadius = GameConfig.HoverRadius; // Distance threshold for hover detection
            const float nameTagOffsetY = GameConfig.NameTagOffsetY; // Position above entity

            // Check enemies
            foreach (var enemy in entityManager.Enemies)
            {
                // Skip enemies that are completely gone (not alive and not dead)
                if (!enemy.IsAlive && !enemy.IsDead)
                    continue;

                float distance = Vector2.Distance(mouseWorldPos, enemy.Position);
                if (distance <= hoverRadius)
                {
                    string name;
                    Color nameColor;
                    
                    // If enemy is dead, show "Corpse" instead of name
                    if (enemy.IsDead)
                    {
                        name = "Corpse";
                        nameColor = Color.Gray; // Gray color for corpses
                    }
                    else
                    {
                        name = enemy._enemyData?.Name ?? "Enemy";
                        if (string.IsNullOrWhiteSpace(name))
                            name = "Enemy";
                        nameColor = Color.Orange; // Orange for alive enemies
                    }
                    
                    DrawNameTag(spriteBatch, enemy.Position + new Vector2(0, nameTagOffsetY), name, nameColor);
                    return; // Only show one name tag at a time
                }
            }

            // Check cameras
            foreach (var camera in entityManager.Cameras)
            {
                float distance = Vector2.Distance(mouseWorldPos, camera.Position);
                if (distance <= hoverRadius)
                {
                    string name = camera._cameraData?.Name ?? "Camera";
                    if (string.IsNullOrWhiteSpace(name))
                        name = "Camera";
                    
                    DrawNameTag(spriteBatch, camera.Position + new Vector2(0, nameTagOffsetY), name, Color.LightBlue);
                    return; // Only show one name tag at a time
                }
            }

            // Check player
            if (entityManager.Player.IsAlive)
            {
                float distance = Vector2.Distance(mouseWorldPos, entityManager.Player.Position);
                if (distance <= hoverRadius)
                {
                    string name = entityManager.Player._playerData?.Name ?? "Player";
                    if (string.IsNullOrWhiteSpace(name))
                        name = "Player";
                    
                    DrawNameTag(spriteBatch, entityManager.Player.Position + new Vector2(0, nameTagOffsetY), name, Color.LightGreen);
                    return; // Only show one name tag at a time
                }
            }
            
            // Check world objects (furniture)
            foreach (var worldObject in _map.WorldObjects)
            {
                float distance = Vector2.Distance(mouseWorldPos, worldObject.Position);
                if (distance <= hoverRadius)
                {
                    string name = worldObject.Name;
                    if (string.IsNullOrWhiteSpace(name))
                        name = "Furniture";
                    
                    // Use yellow color for furniture to distinguish from entities
                    DrawNameTag(spriteBatch, worldObject.Position + new Vector2(0, nameTagOffsetY), name, Color.Yellow);
                    return; // Only show one name tag at a time
                }
            }
        }

        /// <summary>
        /// Draw a single name tag at the specified world position
        /// </summary>
        private void DrawNameTag(SpriteBatch spriteBatch, Vector2 worldPosition, string name, Color nameColor)
        {
            if (_uiFont == null)
                return;

            // Measure text size
            Vector2 textSize = _uiFont.MeasureString(name);
            
            // Calculate text position (centered above entity)
            Vector2 textPos = worldPosition - new Vector2(textSize.X / 2.0f, textSize.Y / 2.0f);
            
            // Create background texture if needed
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(_graphicsDevice, 1, 1);
                _whiteTexture.SetData(new[] { Color.White });
            }
            
            // Draw background rectangle (semi-transparent dark background)
            const float padding = 6.0f;
            Vector2 bgPos = textPos - new Vector2(padding, padding);
            Vector2 bgSize = textSize + new Vector2(padding * 2, padding * 2);
            
            spriteBatch.Draw(
                _whiteTexture,
                bgPos,
                null,
                new Color(0, 0, 0, 180), // Semi-transparent black
                0f,
                Vector2.Zero,
                bgSize,
                SpriteEffects.None,
                0f
            );
            
            // Draw border
            const float borderThickness = 1.0f;
            Color borderColor = new Color(nameColor.R, nameColor.G, nameColor.B, (byte)200);
            
            // Top border
            spriteBatch.Draw(
                _whiteTexture,
                bgPos,
                null,
                borderColor,
                0f,
                Vector2.Zero,
                new Vector2(bgSize.X, borderThickness),
                SpriteEffects.None,
                0f
            );
            
            // Bottom border
            spriteBatch.Draw(
                _whiteTexture,
                bgPos + new Vector2(0, bgSize.Y - borderThickness),
                null,
                borderColor,
                0f,
                Vector2.Zero,
                new Vector2(bgSize.X, borderThickness),
                SpriteEffects.None,
                0f
            );
            
            // Left border
            spriteBatch.Draw(
                _whiteTexture,
                bgPos,
                null,
                borderColor,
                0f,
                Vector2.Zero,
                new Vector2(borderThickness, bgSize.Y),
                SpriteEffects.None,
                0f
            );
            
            // Right border
            spriteBatch.Draw(
                _whiteTexture,
                bgPos + new Vector2(bgSize.X - borderThickness, 0),
                null,
                borderColor,
                0f,
                Vector2.Zero,
                new Vector2(borderThickness, bgSize.Y),
                SpriteEffects.None,
                0f
            );
            
            // Draw text with shadow for better visibility
            spriteBatch.DrawString(_uiFont, name, textPos + new Vector2(1, 1), new Color(0, 0, 0, 200));
            spriteBatch.DrawString(_uiFont, name, textPos, nameColor);
        }

        /// <summary>
        /// Draw the player's equipped weapon
        /// </summary>
        private void DrawPlayerWeapon(SpriteBatch spriteBatch, Player player)
        {
            if (player.EquippedWeapon == null)
                return;
                
            // Create a simple line texture if needed
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(_graphicsDevice, 1, 1);
                _whiteTexture.SetData(new[] { Color.White });
            }
            
            // Calculate direction player is facing
            float rotation = player.Rotation;
            
            // Apply sword swing animation angle if swinging (only for sword)
            if (player.EquippedWeapon is Sword && player.IsSwingingSword)
            {
                rotation += player.GetSwordSwingAngle();
            }
            
            Vector2 direction = new Vector2((float)Math.Cos(rotation), (float)Math.Sin(rotation));
            
            // Determine weapon properties based on type
            float weaponLength;
            float weaponThickness;
            Color weaponColor;
            
            if (player.EquippedWeapon is Gun)
            {
                // Gun: shorter and thicker, yellow color
                weaponLength = 50.0f;
                weaponThickness = 10.0f;
                weaponColor = Color.Yellow;
            }
            else // Sword
            {
                // Sword: longer and thinner, silver color
                weaponLength = 75.0f;
                weaponThickness = 8.0f;
                weaponColor = new Color(220, 220, 255); // Bright silver/white
            }
            
            Vector2 weaponEnd = player.Position + direction * weaponLength;
            
            // Draw weapon as a line extending from player in facing direction
            Vector2 edge = weaponEnd - player.Position;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float lineLength = edge.Length();
            
            if (_whiteTexture != null)
            {
                spriteBatch.Draw(
                    _whiteTexture,
                    player.Position,
                    null,
                    weaponColor,
                    angle,
                    Vector2.Zero,
                    new Vector2(lineLength, weaponThickness),
                    SpriteEffects.None,
                    0.0f
                );
            }
        }
        
        /// <summary>
        /// Draw the pistol's projectile range as a circle around the player (same method as enemy aggro radius)
        /// </summary>
        private void DrawPistolRange(SpriteBatch spriteBatch, Player player)
        {
            if (player.EquippedWeapon is not Gun gun)
                return;
            
            // Calculate projectile range (speed * lifetime)
            const float projectileLifetime = GameConfig.ProjectileLifetime; // 250 / 500 = 0.5 seconds
            float range = gun.ProjectileSpeed * projectileLifetime; // 500 * 0.5 = 250 pixels
            
            int radius = (int)range;
            
            // Create or update circle texture if needed (same as enemy aggro radius)
            if (_pistolRangeCircleTexture == null || _pistolRangeCircleTexture.Width != radius * 2)
            {
                CreatePistolRangeCircleTexture(radius);
            }
            
            if (_pistolRangeCircleTexture == null)
                return;
            
            // Draw the circle (same way as enemy aggro radius)
            Vector2 drawPosition = player.Position - new Vector2(range, range);
            spriteBatch.Draw(_pistolRangeCircleTexture, drawPosition, Color.White);
        }
        
        /// <summary>
        /// Create a circle ring texture for pistol range (same method as enemy aggro radius)
        /// </summary>
        private void CreatePistolRangeCircleTexture(int radius)
        {
            int diameter = radius * 2;
            _pistolRangeCircleTexture = new Texture2D(_graphicsDevice, diameter, diameter);
            Color[] colorData = new Color[diameter * diameter];
            
            Vector2 center = new Vector2(radius, radius);
            
            for (int x = 0; x < diameter; x++)
            {
                for (int y = 0; y < diameter; y++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    
                    // Draw ring (same as enemy aggro radius - 2 pixel thick ring)
                    if (distance >= radius - 2 && distance <= radius)
                    {
                        colorData[y * diameter + x] = new Color(255, 255, 0, 100); // Yellow, semi-transparent
                    }
                    else
                    {
                        colorData[y * diameter + x] = Color.Transparent;
                    }
                }
            }
            
            _pistolRangeCircleTexture.SetData(colorData);
        }
        
        private void DrawDamageNumbers(SpriteBatch spriteBatch)
        {
            // This method is no longer used - damage numbers are drawn in world space
            // Keeping for compatibility
        }
        
        /// <summary>
        /// Extract all faces from a WorldObject's bounding box and add them to the face list for per-face sorting
        /// </summary>
        private void AddWorldObjectBoundingBoxFaces(WorldObject worldObject, List<(Entity? entity, Vector2[] vertices, float depth, Color color)> faceList, float heightScale)
        {
            float halfWidth = worldObject.DiamondWidth / 2.0f;
            float halfHeight = worldObject.DiamondHeight / 2.0f;
            float zOffsetY = worldObject.ZHeight * heightScale;
            
            // Calculate object's base depth (same formula as entity sorting)
            float zOffsetYForDepth = worldObject.ZHeight * heightScale;
            float topFaceCenterY = worldObject.Position.Y - zOffsetYForDepth;
            float objectBaseDepth = (worldObject.Position.X + topFaceCenterY) - (worldObject.ZHeight * 0.3f);
            
            // Add a unique offset based on position to ensure adjacent objects have distinct depths
            // Use a hash of position to create a deterministic but unique offset
            // Range: 0 to 0.01f - large enough to separate objects but small enough to not affect normal sorting
            float positionHash = (worldObject.Position.X * 1000.0f + worldObject.Position.Y * 1000.0f) % 10000.0f;
            float uniqueOffset = (positionHash / 10000.0f) * 0.01f;
            objectBaseDepth += uniqueOffset;
            
            // Calculate all 8 vertices (same as Entity.GetBoundingBoxVertices3D)
            Vector2[] vertices = new Vector2[8];
            
            // Bottom face vertices (z = 0)
            vertices[0] = new Vector2(worldObject.Position.X, worldObject.Position.Y - halfHeight);
            vertices[1] = new Vector2(worldObject.Position.X + halfWidth, worldObject.Position.Y);
            vertices[2] = new Vector2(worldObject.Position.X, worldObject.Position.Y + halfHeight);
            vertices[3] = new Vector2(worldObject.Position.X - halfWidth, worldObject.Position.Y);
            
            // Top face vertices (z = zHeight)
            vertices[4] = new Vector2(worldObject.Position.X, worldObject.Position.Y - halfHeight - zOffsetY);
            vertices[5] = new Vector2(worldObject.Position.X + halfWidth, worldObject.Position.Y - zOffsetY);
            vertices[6] = new Vector2(worldObject.Position.X, worldObject.Position.Y + halfHeight - zOffsetY);
            vertices[7] = new Vector2(worldObject.Position.X - halfWidth, worldObject.Position.Y - zOffsetY);
            
            // Get bounding box color from WorldObject
            Color boxColor = worldObject.BoundingBoxColor;
            
            // For each face, use a fixed offset based on face type
            // These offsets are small (0.0001f increments) and only affect face order within the same object
            // The unique offset ensures different objects have distinct base depths
            // Bottom face (draw first, most back)
            Vector2[] bottomFace = new Vector2[] { vertices[0], vertices[1], vertices[2], vertices[3] };
            faceList.Add((null, bottomFace, objectBaseDepth + 0.0000f, boxColor));
            
            // Top face (draw last, most forward)
            Vector2[] topFace = new Vector2[] { vertices[4], vertices[5], vertices[6], vertices[7] };
            faceList.Add((null, topFace, objectBaseDepth + 0.0005f, boxColor));
            
            // Side faces
            Vector2[] side1 = new Vector2[] { vertices[0], vertices[1], vertices[5], vertices[4] };
            faceList.Add((null, side1, objectBaseDepth + 0.0001f, boxColor));
            
            Vector2[] side2 = new Vector2[] { vertices[1], vertices[2], vertices[6], vertices[5] };
            faceList.Add((null, side2, objectBaseDepth + 0.0002f, boxColor));
            
            Vector2[] side3 = new Vector2[] { vertices[2], vertices[3], vertices[7], vertices[6] };
            faceList.Add((null, side3, objectBaseDepth + 0.0003f, boxColor));
            
            Vector2[] side4 = new Vector2[] { vertices[3], vertices[0], vertices[4], vertices[7] };
            faceList.Add((null, side4, objectBaseDepth + 0.0004f, boxColor));
        }
        
        /// <summary>
        /// Extract all faces from an entity's bounding box and add them to the face list for per-face sorting
        /// </summary>
        private void AddBoundingBoxFaces(Entity entity, List<(Entity? entity, Vector2[] vertices, float depth, Color color)> faceList, float heightScale)
        {
            // Calculate object's base depth (same formula as entity sorting)
            float zOffsetYForDepth = entity.ZHeight * heightScale;
            float topFaceCenterY = entity.Position.Y - zOffsetYForDepth;
            float objectBaseDepth = (entity.Position.X + topFaceCenterY) - (entity.ZHeight * 0.3f);
            
            // Add a unique offset based on position to ensure adjacent objects have distinct depths
            // Use a hash of position to create a deterministic but unique offset
            // Range: 0 to 0.01f - large enough to separate objects but small enough to not affect normal sorting
            float positionHash = (entity.Position.X * 1000.0f + entity.Position.Y * 1000.0f) % 10000.0f;
            float uniqueOffset = (positionHash / 10000.0f) * 0.01f;
            objectBaseDepth += uniqueOffset;
            
            // Get all 8 vertices
            Vector2[] vertices = entity.GetBoundingBoxVertices3D();
            
            // For each face, use a fixed offset based on face type
            // These offsets are small (0.0001f increments) and only affect face order within the same object
            // The unique offset ensures different objects have distinct base depths
            // Bottom face: vertices 0,1,2,3 (draw first, most back)
            Vector2[] bottomFace = new Vector2[] { vertices[0], vertices[1], vertices[2], vertices[3] };
            faceList.Add((entity, bottomFace, objectBaseDepth + 0.0000f, entity.BoundingBoxColor));
            
            // Top face: vertices 4,5,6,7 (draw last, most forward)
            Vector2[] topFace = new Vector2[] { vertices[4], vertices[5], vertices[6], vertices[7] };
            faceList.Add((entity, topFace, objectBaseDepth + 0.0005f, entity.BoundingBoxColor));
            
            // Side faces (4 trapezoids connecting bottom to top)
            // Side 1: bottomTop -> bottomRight -> topRight -> topTop
            Vector2[] side1 = new Vector2[] { vertices[0], vertices[1], vertices[5], vertices[4] };
            faceList.Add((entity, side1, objectBaseDepth + 0.0001f, entity.BoundingBoxColor));
            
            // Side 2: bottomRight -> bottomBottom -> topBottom -> topRight
            Vector2[] side2 = new Vector2[] { vertices[1], vertices[2], vertices[6], vertices[5] };
            faceList.Add((entity, side2, objectBaseDepth + 0.0002f, entity.BoundingBoxColor));
            
            // Side 3: bottomBottom -> bottomLeft -> topLeft -> topBottom
            Vector2[] side3 = new Vector2[] { vertices[2], vertices[3], vertices[7], vertices[6] };
            faceList.Add((entity, side3, objectBaseDepth + 0.0003f, entity.BoundingBoxColor));
            
            // Side 4: bottomLeft -> bottomTop -> topTop -> topLeft
            Vector2[] side4 = new Vector2[] { vertices[3], vertices[0], vertices[4], vertices[7] };
            faceList.Add((entity, side4, objectBaseDepth + 0.0004f, entity.BoundingBoxColor));
        }
        
        /// <summary>
        /// Draw a single bounding box face as a wireframe
        /// </summary>
        private void DrawBoundingBoxFace(SpriteBatch spriteBatch, Texture2D lineTexture, Vector2[] vertices, Color color)
        {
            if (vertices.Length < 2) return;
            
            // Draw lines between consecutive vertices, closing the polygon
            for (int i = 0; i < vertices.Length; i++)
            {
                int next = (i + 1) % vertices.Length;
                DrawBoundingBoxLine(spriteBatch, lineTexture, vertices[i], vertices[next], color);
            }
        }
        
        /// <summary>
        /// Draw a single line for a bounding box face
        /// </summary>
        private void DrawBoundingBoxLine(SpriteBatch spriteBatch, Texture2D texture, Vector2 start, Vector2 end, Color color)
        {
            Vector2 edge = end - start;
            float length = edge.Length();
            
            if (length <= 0.1f) return;
            
            bool isMostlyVertical = Math.Abs(edge.Y) > Math.Abs(edge.X) * 10.0f && Math.Abs(edge.Y) > 0.1f;
            const float boundingBoxLayerDepth = 0.99f;
            const float thickness = 3.0f;
            
            if (isMostlyVertical)
            {
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
        
        private void DrawBloodSplat(SpriteBatch spriteBatch, Vector2 position)
        {
            const float splatSize = 48.0f;
            const float splatOffsetY = 8.0f; // Position slightly below enemy center
            
            // Create blood splat texture if needed
            if (_bloodSplatTexture == null)
            {
                CreateBloodSplatTexture();
            }
            
            if (_bloodSplatTexture != null)
            {
                Vector2 splatPosition = position + new Vector2(0, splatOffsetY) - new Vector2(splatSize / 2.0f, splatSize / 2.0f);
                spriteBatch.Draw(_bloodSplatTexture, splatPosition, Color.White);
            }
        }
        
        private void CreateBloodSplatTexture()
        {
            const int size = 48;
            _bloodSplatTexture = new Texture2D(_graphicsDevice, size, size);
            Color[] colorData = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2.0f, size / 2.0f);
            Random random = new Random(42); // Fixed seed for consistent splat shape
            
            // Create irregular blood splat shape
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    float normalizedDist = distance / (size / 2.0f);
                    
                    // Create irregular shape using noise-like pattern
                    float angle = (float)Math.Atan2(y - center.Y, x - center.X);
                    float radiusVariation = 0.7f + (float)(random.NextDouble() * 0.3f); // Vary radius
                    float baseRadius = (size / 2.0f) * radiusVariation;
                    
                    // Add some spikes/droplets
                    float spikeAngle = angle * 3.0f; // Multiple spikes
                    float spikeAmount = (float)(Math.Sin(spikeAngle) * 0.2f + 1.0f);
                    float effectiveRadius = baseRadius * spikeAmount;
                    
                    if (normalizedDist <= 1.0f)
                    {
                        // Main splat body
                        float alpha = 1.0f - (normalizedDist * 0.8f); // Fade out towards edges
                        alpha = MathHelper.Clamp(alpha, 0f, 1f);
                        
                        // Dark red blood color
                        byte red = (byte)(120 + normalizedDist * 30); // Darker in center
                        byte green = 0;
                        byte blue = 0;
                        byte alphaByte = (byte)(alpha * 200); // Semi-transparent
                        
                        colorData[y * size + x] = new Color(red, green, blue, alphaByte);
                    }
                    else
                    {
                        colorData[y * size + x] = Color.Transparent;
                    }
                }
            }
            
            // Add some random droplets around the splat
            for (int i = 0; i < 8; i++)
            {
                float dropletAngle = (float)(random.NextDouble() * Math.PI * 2);
                float dropletDistance = (size / 2.0f) * (0.6f + (float)(random.NextDouble() * 0.4f));
                int dropletX = (int)(center.X + Math.Cos(dropletAngle) * dropletDistance);
                int dropletY = (int)(center.Y + Math.Sin(dropletAngle) * dropletDistance);
                int dropletSize = 2 + random.Next(3);
                
                for (int dx = -dropletSize; dx <= dropletSize; dx++)
                {
                    for (int dy = -dropletSize; dy <= dropletSize; dy++)
                    {
                        int px = dropletX + dx;
                        int py = dropletY + dy;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                            if (dist <= dropletSize)
                            {
                                colorData[py * size + px] = new Color(150, 0, 0, 180);
                            }
                        }
                    }
                }
            }
            
            _bloodSplatTexture.SetData(colorData);
        }
    }
}

