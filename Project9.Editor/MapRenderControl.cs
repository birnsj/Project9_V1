using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using Project9.Shared;

namespace Project9.Editor
{
    public class MapRenderControl : UserControl
    {
        private EditorCamera _camera;
        private EditorMapData _mapData;
        private TileTextureLoader _textureLoader;
        private TerrainType _selectedTerrainType;
        private readonly HashSet<Keys> _pressedKeys;
        private System.Windows.Forms.Timer _updateTimer;
        private DateTime _lastUpdateTime;
        private Point _mousePosition;
        private int? _hoverTileX;
        private int? _hoverTileY;
        private bool _isDragging;
        private EnemyData? _draggedEnemy;
        private CameraData? _draggedCamera;
        private WeaponData? _draggedWeapon;
        private bool _isDraggingPlayer;
        private PointF _dragOffset;
        private Point _mouseDownPosition; // Track mouse position when button was pressed
        private bool _hasMovedDuringDrag; // Track if mouse moved during drag
        private bool _isPanningCamera = false;
        private Point _panStartMousePosition;
        private bool _showGrid32x16 = false;
        private bool _showGrid64x32 = false;
        private bool _showGrid128x64 = false;
        private bool _showGrid512x256 = false;
        private bool _showGrid1024x512 = false;
        private bool _collisionMode = false;
        private float _gridSnapWidth = 64.0f; // Default grid snap size (64x32)
        private bool _showEnemyCones = true;
        private bool _showCameraCones = true;
        private bool _showBoundingBoxes = true; // Default to on
        private float _boundingBoxOpacity = 0.3f; // Default opacity for bounding boxes (0.0 to 1.0)
        private List<CollisionCellData> _collisionCells = new List<CollisionCellData>();
        private PointF? _collisionHoverPosition = null; // Snapped grid position for collision hover preview
        private float _tileOpacity = 0.7f; // Default opacity for placed tiles (0.0 to 1.0)
        private List<(int x, int y, TerrainType type)>? _cachedTiles = null; // Cached sorted tile list
        private bool _tilesDirty = true; // Flag to indicate tiles need rebuilding
        private bool _pendingInvalidate = false; // Flag to throttle invalidate calls

        public float TileOpacity
        {
            get => _tileOpacity;
            set
            {
                _tileOpacity = Math.Clamp(value, 0.0f, 1.0f);
                Invalidate();
            }
        }

        public float BoundingBoxOpacity
        {
            get => _boundingBoxOpacity;
            set
            {
                _boundingBoxOpacity = Math.Clamp(value, 0.0f, 1.0f);
                Invalidate();
            }
        }

        public bool ShowGrid64x32
        {
            get => _showGrid64x32;
            set
            {
                _showGrid64x32 = value;
                Invalidate();
            }
        }

        public bool ShowGrid32x16
        {
            get => _showGrid32x16;
            set
            {
                _showGrid32x16 = value;
                Invalidate();
            }
        }

        public bool ShowGrid128x64
        {
            get => _showGrid128x64;
            set
            {
                _showGrid128x64 = value;
                Invalidate();
            }
        }

        public bool ShowGrid512x256
        {
            get => _showGrid512x256;
            set
            {
                _showGrid512x256 = value;
                Invalidate();
            }
        }

        public bool ShowGrid1024x512
        {
            get => _showGrid1024x512;
            set
            {
                _showGrid1024x512 = value;
                Invalidate();
            }
        }

        public float GridSnapWidth
        {
            get => _gridSnapWidth;
            set
            {
                _gridSnapWidth = value;
            }
        }

        public float GridSnapHeight => _gridSnapWidth / 2.0f; // Always maintain 2:1 aspect ratio

        public bool CollisionMode
        {
            get => _collisionMode;
            set
            {
                _collisionMode = value;
                if (!value)
                {
                    // Clear collision hover when disabling collision mode
                    _collisionHoverPosition = null;
                }
                Invalidate();
            }
        }

        public bool ShowEnemyCones
        {
            get => _showEnemyCones;
            set
            {
                _showEnemyCones = value;
                Invalidate();
            }
        }

        public bool ShowCameraCones
        {
            get => _showCameraCones;
            set
            {
                _showCameraCones = value;
                Invalidate();
            }
        }
        
        public bool ShowBoundingBoxes
        {
            get => _showBoundingBoxes;
            set
            {
                _showBoundingBoxes = value;
                Invalidate();
            }
        }

        public List<CollisionCellData> CollisionCells => _collisionCells;

        public void ClearAllCollisionCells()
        {
            _collisionCells.Clear();
            SaveCollisionCells();
            Invalidate();
        }

        public TerrainType SelectedTerrainType
        {
            get => _selectedTerrainType;
            set
            {
                _selectedTerrainType = value;
                Invalidate();
            }
        }

        public EditorCamera Camera => _camera;
        public EditorMapData MapData => _mapData;

        /// <summary>
        /// Event raised when an enemy is right-clicked
        /// </summary>
        public event EventHandler<EnemyRightClickedEventArgs>? EnemyRightClicked;

        protected virtual void OnEnemyRightClicked(EnemyData enemy)
        {
            EnemyRightClicked?.Invoke(this, new EnemyRightClickedEventArgs(enemy));
        }
        
        /// <summary>
        /// Event raised when the player is right-clicked
        /// </summary>
        public event EventHandler<PlayerRightClickedEventArgs>? PlayerRightClicked;

        protected virtual void OnPlayerRightClicked(PlayerData player)
        {
            PlayerRightClicked?.Invoke(this, new PlayerRightClickedEventArgs(player));
        }
        
        /// <summary>
        /// Event raised when a camera is right-clicked
        /// </summary>
        public event EventHandler<CameraRightClickedEventArgs>? CameraRightClicked;

        protected virtual void OnCameraRightClicked(CameraData camera, int index)
        {
            CameraRightClicked?.Invoke(this, new CameraRightClickedEventArgs(camera, index));
        }
        
        /// <summary>
        /// Event raised when a weapon is right-clicked
        /// </summary>
        public event EventHandler<WeaponRightClickedEventArgs>? WeaponRightClicked;

        protected virtual void OnWeaponRightClicked(WeaponData weapon)
        {
            WeaponRightClicked?.Invoke(this, new WeaponRightClickedEventArgs(weapon));
        }
        
        /// <summary>
        /// Event raised when an enemy is left-clicked (not dragged)
        /// </summary>
        public event EventHandler<EnemyRightClickedEventArgs>? EnemyLeftClicked;

        protected virtual void OnEnemyLeftClicked(EnemyData enemy)
        {
            EnemyLeftClicked?.Invoke(this, new EnemyRightClickedEventArgs(enemy));
        }
        
        /// <summary>
        /// Event raised when the player is left-clicked (not dragged)
        /// </summary>
        public event EventHandler<PlayerRightClickedEventArgs>? PlayerLeftClicked;

        protected virtual void OnPlayerLeftClicked(PlayerData player)
        {
            PlayerLeftClicked?.Invoke(this, new PlayerRightClickedEventArgs(player));
        }
        
        /// <summary>
        /// Event raised when a camera is left-clicked (not dragged)
        /// </summary>
        public event EventHandler<CameraRightClickedEventArgs>? CameraLeftClicked;

        protected virtual void OnCameraLeftClicked(CameraData camera, int index)
        {
            CameraLeftClicked?.Invoke(this, new CameraRightClickedEventArgs(camera, index));
        }
        
        /// <summary>
        /// Event raised when a weapon is left-clicked (not dragged)
        /// </summary>
        public event EventHandler<WeaponRightClickedEventArgs>? WeaponLeftClicked;

        protected virtual void OnWeaponLeftClicked(WeaponData weapon)
        {
            WeaponLeftClicked?.Invoke(this, new WeaponRightClickedEventArgs(weapon));
        }

        public MapRenderControl()
        {
            _camera = new EditorCamera();
            _mapData = new EditorMapData();
            _textureLoader = new TileTextureLoader();
            _selectedTerrainType = TerrainType.Grass;
            _pressedKeys = new HashSet<Keys>();
            
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
            
            _updateTimer = new System.Windows.Forms.Timer();
            _updateTimer.Interval = 16; // ~60 FPS
            _updateTimer.Tick += UpdateTimer_Tick;
            _lastUpdateTime = DateTime.Now;
            _updateTimer.Start();

            this.MouseWheel += MapRenderControl_MouseWheel;
            this.MouseClick += MapRenderControl_MouseClick;
            this.MouseMove += MapRenderControl_MouseMove;
            this.MouseLeave += MapRenderControl_MouseLeave;
            this.MouseDown += MapRenderControl_MouseDown;
            this.MouseUp += MapRenderControl_MouseUp;
            this.KeyDown += MapRenderControl_KeyDown;
            this.KeyUp += MapRenderControl_KeyUp;
            this.SetStyle(ControlStyles.Selectable, true);
            this.TabStop = true;
            this.MouseEnter += MapRenderControl_MouseEnter;
            this.BackColor = Color.FromArgb(240, 240, 240); // Light grey to match editor form
        }

        private void MapRenderControl_MouseEnter(object? sender, EventArgs e)
        {
            // Auto-focus when mouse enters to enable keyboard input
            if (!this.Focused)
            {
                this.Focus();
            }
        }

        public void Initialize(EditorMapData mapData, TileTextureLoader textureLoader)
        {
            _mapData = mapData;
            _textureLoader = textureLoader;
            
            // Mark tiles as dirty to rebuild cache
            _tilesDirty = true;
            
            // Load collision cells
            LoadCollisionCells();
            
            // Snap player to grid
            SnapPlayerToGrid();
            
            // Snap all enemies to grid
            SnapAllEnemiesToGrid();
            
            // Snap all cameras to grid
            SnapAllCamerasToGrid();
            
            // Center camera on map initially
            CenterCameraOnMap();
            
            Invalidate();
        }

        private void SnapPlayerToGrid()
        {
            // Player is 128x64 diamond (2x the size of a 64x32 grid cell)
            // It should overlay 4 grid cells (2x2), so corners align with grid intersection points
            if (_mapData.MapData.Player != null)
            {
                // Calculate player's bottom corner position
                float playerBottomY = _mapData.MapData.Player.Y + 32.0f;
                PointF playerBottomCorner = new PointF(_mapData.MapData.Player.X, playerBottomY);
                
                // Find nearest grid intersection point to the player's bottom corner
                PointF nearestGridPoint = FindNearestGridPoint(playerBottomCorner);
                
                // Position player center so its bottom corner aligns with the grid intersection point
                // This ensures the 128x64 diamond overlays 4 grid cells (2x2)
                _mapData.MapData.Player.X = nearestGridPoint.X;
                _mapData.MapData.Player.Y = nearestGridPoint.Y - 32.0f;
            }
        }

        private void SnapAllEnemiesToGrid()
        {
            // Enemies are 128x64 diamonds (same as player), should overlay 4 grid cells (2x2)
            foreach (var enemy in _mapData.MapData.Enemies)
            {
                // Calculate enemy's bottom corner position
                float enemyBottomY = enemy.Y + 32.0f;
                PointF enemyBottomCorner = new PointF(enemy.X, enemyBottomY);
                
                // Find nearest grid intersection point to the enemy's bottom corner
                PointF nearestGridPoint = FindNearestGridPoint(enemyBottomCorner);
                
                // Position enemy center so its bottom corner aligns with the grid intersection point
                // This ensures the 128x64 diamond overlays 4 grid cells (2x2)
                enemy.X = nearestGridPoint.X;
                enemy.Y = nearestGridPoint.Y - 32.0f;
            }
        }

        private void SnapAllCamerasToGrid()
        {
            // Cameras are 128x64 diamonds (same as player and enemy), should overlay 4 grid cells (2x2)
            foreach (var camera in _mapData.MapData.Cameras)
            {
                // Calculate camera's bottom corner position
                float cameraBottomY = camera.Y + 32.0f;
                PointF cameraBottomCorner = new PointF(camera.X, cameraBottomY);
                
                // Find nearest grid intersection point to the camera's bottom corner
                PointF nearestGridPoint = FindNearestGridPoint(cameraBottomCorner);
                
                // Position camera center so its bottom corner aligns with the grid intersection point
                // This ensures the 128x64 diamond overlays 4 grid cells (2x2)
                camera.X = nearestGridPoint.X;
                camera.Y = nearestGridPoint.Y - 32.0f;
            }
        }

        private void LoadCollisionCells()
        {
            string collisionPath = "Content/world/collision.json";
            string? resolvedPath = ResolveCollisionPath(collisionPath);
            
            if (resolvedPath != null && File.Exists(resolvedPath))
            {
                try
                {
                    string json = File.ReadAllText(resolvedPath);
                    var collisionData = System.Text.Json.JsonSerializer.Deserialize<CollisionData>(json);
                    if (collisionData?.Cells != null)
                    {
                        _collisionCells = collisionData.Cells;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MapRenderControl] Error loading collision cells: {ex.Message}");
                }
            }
        }

        private static string? ResolveCollisionPath(string relativePath)
        {
            string currentDir = Directory.GetCurrentDirectory();
            string fullPath = Path.GetFullPath(Path.Combine(currentDir, relativePath));
            if (File.Exists(fullPath))
                return fullPath;

            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            fullPath = Path.GetFullPath(Path.Combine(exeDir, relativePath));
            if (File.Exists(fullPath))
                return fullPath;

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

        private void SaveCollisionCells()
        {
            string collisionPath = "Content/world/collision.json";
            string? resolvedPath = ResolveCollisionPath(collisionPath);
            
            if (resolvedPath == null)
            {
                // Try to create the directory if it doesn't exist
                string dir = Path.GetDirectoryName(collisionPath) ?? "Content/world";
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                resolvedPath = Path.GetFullPath(collisionPath);
            }

            try
            {
                var collisionData = new CollisionData { Cells = _collisionCells };
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string json = System.Text.Json.JsonSerializer.Serialize(collisionData, options);
                File.WriteAllText(resolvedPath, json);
                Console.WriteLine($"[MapRenderControl] Saved collision cells to {resolvedPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MapRenderControl] Error saving collision cells: {ex.Message}");
            }
        }

        private void CenterCameraOnMap()
        {
            if (_mapData == null || _mapData.Width == 0 || _mapData.Height == 0)
                return;

            // Calculate center of the map in screen coordinates
            float centerTileX = (_mapData.Width - 1) / 2.0f;
            float centerTileY = (_mapData.Height - 1) / 2.0f;
            var (centerScreenX, centerScreenY) = IsometricMath.TileToScreen((int)centerTileX, (int)centerTileY);
            
            // Offset by control center to properly center the view
            // Use actual control size or default to 800x600 if not yet initialized
            float screenCenterX = (this.Width > 0 ? this.Width : 800) / 2.0f;
            float screenCenterY = (this.Height > 0 ? this.Height : 600) / 2.0f;
            
            _camera.Position = new PointF(
                centerScreenX - screenCenterX / _camera.Zoom,
                centerScreenY - screenCenterY / _camera.Zoom
            );
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            DateTime currentTime = DateTime.Now;
            float deltaTime = (float)(currentTime - _lastUpdateTime).TotalSeconds;
            _lastUpdateTime = currentTime;

            // Handle WASD input for panning
            PointF panDirection = PointF.Empty;
            
            if (_pressedKeys.Contains(Keys.W))
                panDirection.Y -= 1;
            if (_pressedKeys.Contains(Keys.S))
                panDirection.Y += 1;
            if (_pressedKeys.Contains(Keys.A))
                panDirection.X -= 1;
            if (_pressedKeys.Contains(Keys.D))
                panDirection.X += 1;

            if (panDirection.X != 0 || panDirection.Y != 0)
            {
                PointF oldPos = _camera.Position;
                _camera.Pan(panDirection, deltaTime);
                // Only invalidate if camera position actually changed
                if (oldPos != _camera.Position)
                {
                    RequestInvalidate();
                }
            }
        }

        private void MapRenderControl_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W || e.KeyCode == Keys.A || e.KeyCode == Keys.S || e.KeyCode == Keys.D)
            {
                _pressedKeys.Add(e.KeyCode);
                e.Handled = true;
            }
        }

        private void MapRenderControl_KeyUp(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W || e.KeyCode == Keys.A || e.KeyCode == Keys.S || e.KeyCode == Keys.D)
            {
                _pressedKeys.Remove(e.KeyCode);
                e.Handled = true;
            }
        }

        private void MapRenderControl_MouseWheel(object? sender, MouseEventArgs e)
        {
            // Allow mouse wheel scrolling when mouse is over the control, regardless of focus
            // This fixes the issue where dragging a window causes focus to shift and breaks scrolling
            // Get mouse position in screen coordinates
            Point mouseScreen = e.Location;
            
            // Convert to world coordinates before zoom
            PointF mouseWorldBefore = ScreenToWorld(mouseScreen);
            
            // Calculate zoom amount (use a percentage-based zoom for smoother feel)
            float zoomFactor = e.Delta > 0 ? 1.1f : 1.0f / 1.1f;
            float oldZoom = _camera.Zoom;
            float newZoom = Math.Clamp(oldZoom * zoomFactor, 0.5f, 4.0f);
            
            // Only apply if zoom actually changed (within limits)
            if (Math.Abs(newZoom - oldZoom) > 0.001f)
            {
                _camera.Zoom = newZoom;
                
                // Convert mouse position to world coordinates after zoom
                PointF mouseWorldAfter = ScreenToWorld(mouseScreen);
                
                // Adjust camera position to keep the mouse point in the same world position
                PointF worldOffset = new PointF(
                    mouseWorldBefore.X - mouseWorldAfter.X,
                    mouseWorldBefore.Y - mouseWorldAfter.Y
                );
                
                _camera.Position = new PointF(
                    _camera.Position.X + worldOffset.X * _camera.Zoom,
                    _camera.Position.Y + worldOffset.Y * _camera.Zoom
                );
                
                Invalidate();
            }
        }

        private void MapRenderControl_MouseMove(object? sender, MouseEventArgs e)
        {
            _mousePosition = e.Location;
            
            // Track if mouse moved during drag (to distinguish click from drag)
            if (_isDragging && !_hasMovedDuringDrag)
            {
                int deltaX = Math.Abs(e.X - _mouseDownPosition.X);
                int deltaY = Math.Abs(e.Y - _mouseDownPosition.Y);
                if (deltaX > 3 || deltaY > 3) // Small threshold to ignore tiny movements
                {
                    _hasMovedDuringDrag = true;
                }
            }
            
            if (_isDragging)
            {
                PointF worldPos = ScreenToWorld(e.Location);
                PointF targetPos = new PointF(worldPos.X - _dragOffset.X, worldPos.Y - _dragOffset.Y);
                
                // Snap to 64x32 grid
                targetPos = SnapToGrid(targetPos);
                
                if (_draggedWeapon != null)
                {
                    _draggedWeapon.X = targetPos.X;
                    _draggedWeapon.Y = targetPos.Y;
                    Invalidate();
                }
                else if (_isDraggingPlayer && _mapData.MapData.Player != null)
                {
                    // Player is 128x64 diamond, should overlay 4 grid cells (2x2)
                    // Calculate player's bottom corner position (where we want to snap)
                    float playerBottomY = targetPos.Y + 32.0f;
                    PointF playerBottomCorner = new PointF(targetPos.X, playerBottomY);
                    
                    // Find nearest grid intersection point to the player's bottom corner
                    PointF nearestGridPoint = FindNearestGridPoint(playerBottomCorner);
                    
                    // Position player center so its bottom corner aligns with the grid intersection point
                    // This ensures all 4 corners align with grid points for a 2x2 overlay
                    _mapData.MapData.Player.X = nearestGridPoint.X;
                    _mapData.MapData.Player.Y = nearestGridPoint.Y - 32.0f;
                    Invalidate();
                }
                else if (_draggedEnemy != null)
                {
                    // Enemies are 128x64 diamonds (same as player), should overlay 4 grid cells (2x2)
                    // Calculate enemy's bottom corner position (where we want to snap)
                    float enemyBottomY = targetPos.Y + 32.0f;
                    PointF enemyBottomCorner = new PointF(targetPos.X, enemyBottomY);
                    
                    // Find nearest grid intersection point to the enemy's bottom corner
                    PointF nearestGridPoint = FindNearestGridPoint(enemyBottomCorner);
                    
                    // Position enemy center so its bottom corner aligns with the grid intersection point
                    // This ensures all 4 corners align with grid points for a 2x2 overlay
                    _draggedEnemy.X = nearestGridPoint.X;
                    _draggedEnemy.Y = nearestGridPoint.Y - 32.0f;
                    Invalidate();
                }
                else if (_draggedCamera != null)
                {
                    // Cameras are 128x64 diamonds (same as player and enemy), should overlay 4 grid cells (2x2)
                    // Calculate camera's bottom corner position (where we want to snap)
                    float cameraBottomY = targetPos.Y + 32.0f;
                    PointF cameraBottomCorner = new PointF(targetPos.X, cameraBottomY);
                    
                    // Find nearest grid intersection point to the camera's bottom corner
                    PointF nearestGridPoint = FindNearestGridPoint(cameraBottomCorner);
                    
                    // Position camera center so its bottom corner aligns with the grid intersection point
                    // This ensures all 4 corners align with grid points for a 2x2 overlay
                    _draggedCamera.X = nearestGridPoint.X;
                    _draggedCamera.Y = nearestGridPoint.Y - 32.0f;
                    Invalidate();
                }
            }
            else if (_isPanningCamera)
            {
                // Calculate mouse delta
                int deltaX = e.X - _panStartMousePosition.X;
                int deltaY = e.Y - _panStartMousePosition.Y;
                
                // Convert screen delta to world delta (accounting for zoom)
                // Moving mouse right should move camera left (pan right in world)
                float worldDeltaX = -deltaX / _camera.Zoom;
                float worldDeltaY = -deltaY / _camera.Zoom;
                
                // Update camera position
                _camera.Position = new PointF(
                    _camera.Position.X + worldDeltaX,
                    _camera.Position.Y + worldDeltaY
                );
                
                // Update start position for next move
                _panStartMousePosition = e.Location;
                
                Invalidate();
            }
            else if (_collisionMode)
            {
                // Update collision hover preview position (snapped to grid)
                PointF worldPos = ScreenToWorld(e.Location);
                PointF snappedPos = SnapToGrid(worldPos);
                
                if (!_collisionHoverPosition.HasValue || 
                    Math.Abs(_collisionHoverPosition.Value.X - snappedPos.X) > 0.1f || 
                    Math.Abs(_collisionHoverPosition.Value.Y - snappedPos.Y) > 0.1f)
                {
                    _collisionHoverPosition = snappedPos;
                    Invalidate();
                }
                
                // Clear tile hover when in collision mode
                if (_hoverTileX.HasValue || _hoverTileY.HasValue)
                {
                    _hoverTileX = null;
                    _hoverTileY = null;
                }
            }
            else
            {
                // Clear collision hover when not in collision mode
                if (_collisionHoverPosition.HasValue)
                {
                    _collisionHoverPosition = null;
                    Invalidate();
                }
                
                // Update tile hover for normal tile editing
                UpdateHoveredTile(e.Location);
            }
        }

        private void MapRenderControl_MouseLeave(object? sender, EventArgs e)
        {
            _hoverTileX = null;
            _hoverTileY = null;
            Invalidate();
        }

        private void UpdateHoveredTile(Point screenPoint)
        {
            // Convert screen to world coordinates
            PointF worldPos = ScreenToWorld(screenPoint);
            
            // Get approximate tile first
            var (approxTileX, approxTileY) = IsometricMath.ScreenToTile(worldPos.X, worldPos.Y);
            
            // Find the tile whose bottom center is closest to the cursor
            // The bottom center of an isometric diamond is at (screenX, screenY + TileHeight)
            int? foundTileX = null;
            int? foundTileY = null;
            float minDistance = float.MaxValue;
            
            // Check tiles in a small area around the approximate position
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    int testX = approxTileX + dx;
                    int testY = approxTileY + dy;
                    
                    if (testX >= 0 && testX < _mapData.Width && testY >= 0 && testY < _mapData.Height)
                    {
                        // Get tile coordinate point (grid point)
                        var (tileScreenX, tileScreenY) = IsometricMath.TileToScreen(testX, testY);
                        
                        // The grid point is where we want the bottom center to align
                        // Distance from cursor to this tile's grid point (where bottom center will be)
                        float dx2 = worldPos.X - tileScreenX;
                        float dy2 = worldPos.Y - tileScreenY;
                        float distance = (float)Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                        
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            foundTileX = testX;
                            foundTileY = testY;
                        }
                    }
                }
            }
            
            // Use found tile or approximate
            int tileX = foundTileX ?? approxTileX;
            int tileY = foundTileY ?? approxTileY;
            
            // Update hovered tile if changed
            if (tileX >= 0 && tileX < _mapData.Width && tileY >= 0 && tileY < _mapData.Height)
            {
                if (_hoverTileX != tileX || _hoverTileY != tileY)
                {
                    _hoverTileX = tileX;
                    _hoverTileY = tileY;
                    Invalidate();
                }
            }
            else
            {
                if (_hoverTileX.HasValue || _hoverTileY.HasValue)
                {
                    _hoverTileX = null;
                    _hoverTileY = null;
                    Invalidate();
                }
            }
        }

        private PointF FindNearestGridPoint(PointF position)
        {
            float gridCellWidth = _gridSnapWidth;
            float gridCellHalfHeight = GridSnapHeight / 2.0f;
            
            // Grid cells per tile
            int gridCellsPerTile = (int)(IsometricMath.TileWidth / gridCellWidth);
            
            // Find the nearest grid intersection point (corner where grid cell diamonds meet)
            float minDistance = float.MaxValue;
            PointF nearestGridPoint = position;
            
            // Check nearby tiles
            var (tileX, tileY) = IsometricMath.ScreenToTile(position.X, position.Y);
            
            for (int dtX = -1; dtX <= 1; dtX++)
            {
                for (int dtY = -1; dtY <= 1; dtY++)
                {
                    var (tileScreenX, tileScreenY) = IsometricMath.TileToScreen(tileX + dtX, tileY + dtY);
                    
                    // Calculate all grid intersection points in this tile
                    // Grid intersection points are the corners of grid cell diamonds
                    // Each grid cell is 64x32, so intersections occur at regular intervals
                    for (int gridCellX = 0; gridCellX <= gridCellsPerTile; gridCellX++)
                    {
                        for (int gridCellY = 0; gridCellY <= gridCellsPerTile; gridCellY++)
                        {
                            // Calculate grid cell center position
                            float cellProgressX = gridCellX / (float)gridCellsPerTile;
                            float cellProgressY = gridCellY / (float)gridCellsPerTile;
                            
                            // Offset from tile corner to grid cell position
                            float cellOffsetX = (cellProgressX - cellProgressY) * (IsometricMath.TileWidth / 2.0f);
                            float cellOffsetY = (cellProgressX + cellProgressY) * (IsometricMath.TileHeight / 2.0f);
                            
                            // Now, for each grid cell, its corners are intersection points
                            // The bottom corner of a grid cell is at (centerX, centerY + halfHeight)
                            // But we need to think about this differently: intersection points are
                            // where cell corners meet, which means they're at the cell corners themselves
                            
                            // Let's calculate the intersection point as the bottom corner of the grid cell
                            // that would be centered at this position
                            // Actually, grid intersection points form a regular pattern
                            // For a cell centered at (cx, cy), its bottom corner is (cx, cy + 16)
                            // So if we calculate where cells would be centered and add the offset...
                            
                            // Better approach: calculate the intersection point directly
                            // In isometric space, grid intersections are spaced by grid cell dimensions
                            // The intersection point at (gridCellX, gridCellY) represents a corner
                            // Position it as if it's the bottom corner of a cell at this grid position
                            
                            // Calculate the position of the intersection point
                            // This is the corner where grid lines meet
                            float intersectionX = tileScreenX + cellOffsetX;
                            float intersectionY = tileScreenY + cellOffsetY + gridCellHalfHeight;
                            
                            // However, we need to account for the fact that intersections form a pattern
                            // Let's try a different approach: calculate where a grid cell's bottom corner
                            // would be if the cell center is at this grid position
                            
                            // Actually, let's think about it this way:
                            // Grid cells are positioned at their centers
                            // Grid intersection points are where the corners of these cells meet
                            // The bottom corner of a cell at center (cx, cy) is at (cx, cy + 16)
                            // So if we want to find intersection points, we can think of them as
                            // the corners of cells, which means we need cell centers
                            
                            // Calculate grid cell center position using +0.5 offset for center
                            // Grid cells are positioned at their centers, and we want to find their corner points
                            float cellCenterProgressX = (gridCellX + 0.5f) / gridCellsPerTile;
                            float cellCenterProgressY = (gridCellY + 0.5f) / gridCellsPerTile;
                            
                            float cellCenterOffsetX = (cellCenterProgressX - cellCenterProgressY) * (IsometricMath.TileWidth / 2.0f);
                            float cellCenterOffsetY = (cellCenterProgressX + cellCenterProgressY) * (IsometricMath.TileHeight / 2.0f);
                            
                            float cellCenterX = tileScreenX + cellCenterOffsetX;
                            float cellCenterY = tileScreenY + cellCenterOffsetY;
                            
                            // The bottom corner of this grid cell is a grid intersection point
                            // This is where the 128x64 player diamond should align its bottom corner
                            float gridPointX = cellCenterX;
                            float gridPointY = cellCenterY + gridCellHalfHeight;
                            
                            float distance = (float)Math.Sqrt(Math.Pow(position.X - gridPointX, 2) + Math.Pow(position.Y - gridPointY, 2));
                            
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                nearestGridPoint = new PointF(gridPointX, gridPointY);
                            }
                        }
                    }
                }
            }
            
            return nearestGridPoint;
        }

        private PointF SnapToGrid(PointF position)
        {
            // Default to 64x32 diamond (halfHeight = 16.0f) for collisions and other entities
            return SnapToGrid(position, 16.0f);
        }

        private PointF SnapToGrid(PointF position, float entityHalfHeight)
        {
            float gridX = _gridSnapWidth;
            
            // Grid cells per tile
            int gridCellsPerTile = (int)(IsometricMath.TileWidth / gridX);
            
            // Find the nearest grid cell bottom corner by checking nearby positions
            float minDistance = float.MaxValue;
            PointF nearestCellBottomCorner = position;
            
            // Check nearby tiles
            var (tileX, tileY) = IsometricMath.ScreenToTile(position.X, position.Y);
            
            for (int dtX = -1; dtX <= 1; dtX++)
            {
                for (int dtY = -1; dtY <= 1; dtY++)
                {
                    var (tileScreenX, tileScreenY) = IsometricMath.TileToScreen(tileX + dtX, tileY + dtY);
                    
                    // Check all grid cells in this tile
                    for (int gridCellX = 0; gridCellX < gridCellsPerTile; gridCellX++)
                    {
                        for (int gridCellY = 0; gridCellY < gridCellsPerTile; gridCellY++)
                        {
                            // Calculate grid cell center position
                            float progressX = (gridCellX + 0.5f) / gridCellsPerTile;
                            float progressY = (gridCellY + 0.5f) / gridCellsPerTile;
                            
                            float cellOffsetX = (progressX - progressY) * (IsometricMath.TileWidth / 2.0f);
                            float cellOffsetY = (progressX + progressY) * (IsometricMath.TileHeight / 2.0f);
                            
                            float cellCenterX = tileScreenX + cellOffsetX;
                            float cellCenterY = tileScreenY + cellOffsetY;
                            
                            // Bottom corner of grid cell is at the bottom point of the diamond
                            float gridHalfHeight = GridSnapHeight / 2.0f;
                            // Bottom point: (centerX, centerY + gridHalfHeight)
                            float cellBottomX = cellCenterX;
                            float cellBottomY = cellCenterY + gridHalfHeight;
                            
                            float distance = (float)Math.Sqrt(Math.Pow(position.X - cellBottomX, 2) + Math.Pow(position.Y - cellBottomY, 2));
                            
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                nearestCellBottomCorner = new PointF(cellBottomX, cellBottomY);
                            }
                        }
                    }
                }
            }
            
            // Return position where entity center should be so its bottom point is at the grid cell bottom corner
            // Entity bottom point is at (centerX, centerY + entityHalfHeight)
            // We want: entityCenterY + entityHalfHeight = gridCellBottomY
            // So: entityCenterY = gridCellBottomY - entityHalfHeight
            return new PointF(nearestCellBottomCorner.X, nearestCellBottomCorner.Y - entityHalfHeight);
        }

        private void MapRenderControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                // Start panning the camera
                _isPanningCamera = true;
                _panStartMousePosition = e.Location;
                this.Cursor = Cursors.Hand;
                return;
            }
            
            if (e.Button == MouseButtons.Left)
            {
                _mouseDownPosition = e.Location;
                _hasMovedDuringDrag = false;
                PointF worldPos = ScreenToWorld(e.Location);
                
                // Check if clicking on weapon (check weapons first since they're smaller)
                if (_mapData.MapData.Weapons != null)
                {
                    foreach (var weapon in _mapData.MapData.Weapons)
                    {
                        float weaponScreenX = weapon.X;
                        float weaponScreenY = weapon.Y;
                        float distance = (float)Math.Sqrt(Math.Pow(worldPos.X - weaponScreenX, 2) + Math.Pow(worldPos.Y - weaponScreenY, 2));
                        if (distance < 50) // Click radius
                        {
                            _draggedWeapon = weapon;
                            _isDragging = true;
                            _dragOffset = new PointF(worldPos.X - weaponScreenX, worldPos.Y - weaponScreenY);
                            Invalidate();
                            return;
                        }
                    }
                }
                
                // Check if clicking on player
                if (_mapData.MapData.Player != null)
                {
                    float playerScreenX = _mapData.MapData.Player.X;
                    float playerScreenY = _mapData.MapData.Player.Y;
                    float distance = (float)Math.Sqrt(Math.Pow(worldPos.X - playerScreenX, 2) + Math.Pow(worldPos.Y - playerScreenY, 2));
                    if (distance < 50) // Click radius
                    {
                        _isDraggingPlayer = true;
                        _isDragging = true;
                        _dragOffset = new PointF(worldPos.X - playerScreenX, worldPos.Y - playerScreenY);
                        Invalidate();
                        return;
                    }
                }
                
                // Check if clicking on any enemy
                foreach (var enemy in _mapData.MapData.Enemies)
                {
                    float enemyScreenX = enemy.X;
                    float enemyScreenY = enemy.Y;
                    float distance = (float)Math.Sqrt(Math.Pow(worldPos.X - enemyScreenX, 2) + Math.Pow(worldPos.Y - enemyScreenY, 2));
                    if (distance < 50) // Click radius
                    {
                        _draggedEnemy = enemy;
                        _isDragging = true;
                        _dragOffset = new PointF(worldPos.X - enemyScreenX, worldPos.Y - enemyScreenY);
                        Invalidate();
                        return;
                    }
                }
                
                // Check if clicking on any camera
                foreach (var camera in _mapData.MapData.Cameras)
                {
                    float cameraScreenX = camera.X;
                    float cameraScreenY = camera.Y;
                    float distance = (float)Math.Sqrt(Math.Pow(worldPos.X - cameraScreenX, 2) + Math.Pow(worldPos.Y - cameraScreenY, 2));
                    if (distance < 50) // Click radius
                    {
                        _draggedCamera = camera;
                        _isDragging = true;
                        _dragOffset = new PointF(worldPos.X - cameraScreenX, worldPos.Y - cameraScreenY);
                        Invalidate();
                        return;
                    }
                }
                
                // If not dragging, handle clicks
                if (!_isDragging)
                {
                    if (_collisionMode)
                    {
                        // Left click: Place collision cell
                        PointF snappedPos = SnapToGrid(worldPos);
                        
                        // Check if there's already a collision cell at this position
                        var existingCell = _collisionCells.FirstOrDefault(c => 
                            Math.Abs(c.X - snappedPos.X) < 1 && Math.Abs(c.Y - snappedPos.Y) < 1);
                        
                        if (existingCell == null)
                        {
                            // Add collision cell
                            _collisionCells.Add(new CollisionCellData { X = snappedPos.X, Y = snappedPos.Y });
                            SaveCollisionCells();
                            Invalidate();
                        }
                    }
                    else
                    {
                        // Use the hovered tile coordinates if available (matches the preview)
                        if (_hoverTileX.HasValue && _hoverTileY.HasValue)
                        {
                            _mapData.SetTile(_hoverTileX.Value, _hoverTileY.Value, _selectedTerrainType);
                            _tilesDirty = true; // Mark tiles as dirty when modified
                            Invalidate();
                        }
                        else
                        {
                            // Fallback: calculate from mouse position
                            var (tileX, tileY) = IsometricMath.ScreenToTile(worldPos.X, worldPos.Y);
                            
                            if (tileX >= 0 && tileX < _mapData.Width && tileY >= 0 && tileY < _mapData.Height)
                            {
                                _mapData.SetTile(tileX, tileY, _selectedTerrainType);
                                _tilesDirty = true; // Mark tiles as dirty when modified
                                Invalidate();
                            }
                        }
                    }
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                PointF worldPos = ScreenToWorld(e.Location);
                
                // Right click: Delete collision cell (only in collision mode)
                if (_collisionMode)
                {
                    PointF snappedPos = SnapToGrid(worldPos);
                    
                    // Find and remove collision cell at this position
                    var existingCell = _collisionCells.FirstOrDefault(c => 
                        Math.Abs(c.X - snappedPos.X) < 1 && Math.Abs(c.Y - snappedPos.Y) < 1);
                    
                    if (existingCell != null)
                    {
                        _collisionCells.Remove(existingCell);
                        SaveCollisionCells();
                        Invalidate();
                    }
                }
                else
                {
                    // Right click on player: Open properties window
                    if (_mapData.MapData.Player != null)
                    {
                        float playerScreenX = _mapData.MapData.Player.X;
                        float playerScreenY = _mapData.MapData.Player.Y;
                        float distance = (float)Math.Sqrt(Math.Pow(worldPos.X - playerScreenX, 2) + Math.Pow(worldPos.Y - playerScreenY, 2));
                        if (distance < 50) // Click radius
                        {
                            OnPlayerRightClicked(_mapData.MapData.Player);
                            return;
                        }
                    }
                    
                    // Right click on camera: Open properties window
                    for (int i = 0; i < _mapData.MapData.Cameras.Count; i++)
                    {
                        var camera = _mapData.MapData.Cameras[i];
                        float cameraScreenX = camera.X;
                        float cameraScreenY = camera.Y;
                        float distance = (float)Math.Sqrt(Math.Pow(worldPos.X - cameraScreenX, 2) + Math.Pow(worldPos.Y - cameraScreenY, 2));
                        if (distance < 50) // Click radius
                        {
                            OnCameraRightClicked(camera, i);
                            return;
                        }
                    }
                    
                    // Right click on weapon: Open properties window
                    if (_mapData.MapData.Weapons != null)
                    {
                        WeaponData? clickedWeapon = null;
                        foreach (var weapon in _mapData.MapData.Weapons)
                        {
                            float weaponScreenX = weapon.X;
                            float weaponScreenY = weapon.Y;
                            float distance = (float)Math.Sqrt(Math.Pow(worldPos.X - weaponScreenX, 2) + Math.Pow(worldPos.Y - weaponScreenY, 2));
                            if (distance < 50) // Click radius
                            {
                                clickedWeapon = weapon;
                                break;
                            }
                        }
                        
                        // Raise event for weapon right-click
                        if (clickedWeapon != null)
                        {
                            OnWeaponRightClicked(clickedWeapon);
                            return;
                        }
                    }
                    
                    // Right click on enemy: Open properties window
                    // Check if clicking on any enemy
                    EnemyData? clickedEnemy = null;
                    foreach (var enemy in _mapData.MapData.Enemies)
                    {
                        float enemyScreenX = enemy.X;
                        float enemyScreenY = enemy.Y;
                        float distance = (float)Math.Sqrt(Math.Pow(worldPos.X - enemyScreenX, 2) + Math.Pow(worldPos.Y - enemyScreenY, 2));
                        if (distance < 50) // Click radius
                        {
                            clickedEnemy = enemy;
                            break;
                        }
                    }
                    
                    // Raise event for enemy right-click
                    if (clickedEnemy != null)
                    {
                        OnEnemyRightClicked(clickedEnemy);
                    }
                }
            }
        }

        private void MapRenderControl_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                // Stop panning the camera
                _isPanningCamera = false;
                this.Cursor = Cursors.Default;
                Invalidate();
                return;
            }
            
            if (e.Button == MouseButtons.Left)
            {
                // Check if this was a click (not a drag) on an entity
                // Only treat as click if we were dragging an entity but didn't move the mouse
                if (_isDragging && !_hasMovedDuringDrag)
                {
                    // This was a click, not a drag - check which entity was clicked
                    PointF worldPos = ScreenToWorld(e.Location);
                    
                    // Check if clicking on weapon (check weapons first since they're smaller)
                    if (_draggedWeapon != null)
                    {
                        OnWeaponLeftClicked(_draggedWeapon);
                    }
                    // Check if clicking on player
                    else if (_isDraggingPlayer && _mapData.MapData.Player != null)
                    {
                        OnPlayerLeftClicked(_mapData.MapData.Player);
                    }
                    // Check if clicking on any camera
                    else if (_draggedCamera != null)
                    {
                        int cameraIndex = _mapData.MapData.Cameras.IndexOf(_draggedCamera);
                        if (cameraIndex >= 0)
                        {
                            OnCameraLeftClicked(_draggedCamera, cameraIndex);
                        }
                    }
                    // Check if clicking on any enemy
                    else if (_draggedEnemy != null)
                    {
                        OnEnemyLeftClicked(_draggedEnemy);
                    }
                }
                
                _isDragging = false;
                _isDraggingPlayer = false;
                _draggedEnemy = null;
                _draggedCamera = null;
                _draggedWeapon = null;
                _hasMovedDuringDrag = false;
                Invalidate();
            }
        }

        private void MapRenderControl_MouseClick(object? sender, MouseEventArgs e)
        {
            // Click handling is now in MouseDown
        }

        private void DrawHoverPreview(Graphics g)
        {
            if (_hoverTileX == null || _hoverTileY == null)
                return;

            // Use the same drawing method as regular tiles
            var (screenX, screenY) = IsometricMath.TileToScreen(_hoverTileX.Value, _hoverTileY.Value);
            Bitmap? texture = _textureLoader.GetTexture(_selectedTerrainType);
            
            if (texture != null)
            {
                // Draw semi-transparent preview (same position as regular tiles)
                using (System.Drawing.Imaging.ImageAttributes imageAttributes = new System.Drawing.Imaging.ImageAttributes())
                {
                    // For hover preview, use 0.5f opacity but preserve original alpha channel
                    System.Drawing.Imaging.ColorMatrix colorMatrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
                    {
                        new float[] {1, 0, 0, 0, 0},
                        new float[] {0, 1, 0, 0, 0},
                        new float[] {0, 0, 1, 0, 0},
                        new float[] {0, 0, 0, 0.5f, 0}, // Multiply alpha by 0.5 for hover preview
                        new float[] {0, 0, 0, 0, 1}
                    });
                    imageAttributes.SetColorMatrix(colorMatrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
                    imageAttributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY); // Prevent edge artifacts
                    
                    // All tiles place at grid corner point
                    // screenX, screenY is the grid corner (top of isometric diamond)
                    // Draw tiles at their natural position - top of diamond at grid corner
                    
                    if (_selectedTerrainType == TerrainType.Test || _selectedTerrainType == TerrainType.Test2)
                    {
                        // Test tiles: 1024x1024, but bottom 1024x512 is the diamond, top 512 is overdraw
                        // Grid corner (screenX, screenY) should align with bottom of diamond
                        // TileToScreen returns the top point, which is centered horizontally
                        // Offset upward by TileHeight + overdraw to align bottom diamond with grid corner
                        float overdrawHeight = texture.Height - IsometricMath.TileHeight; // 512 for Test tile
                        float totalOffset = IsometricMath.TileHeight + overdrawHeight; // 512 + 512 = 1024
                        
                        // Center horizontally: TileToScreen returns center, so offset by half width
                        float drawX = screenX - (texture.Width / 2.0f);
                        float drawY = screenY - totalOffset; // Move up to align bottom diamond
                        
                        // Round to nearest pixel for perfect alignment
                        int finalDrawX = (int)Math.Round(drawX);
                        int finalDrawY = (int)Math.Round(drawY);
                        
                        g.DrawImage(
                            texture,
                            new Rectangle(finalDrawX, finalDrawY, texture.Width, texture.Height),
                            0, 0, texture.Width, texture.Height,
                            System.Drawing.GraphicsUnit.Pixel,
                            imageAttributes);
                    }
                    else
                    {
                        // Regular tiles: align bottom of diamond with grid corner
                        // Grid corner (screenX, screenY) should align with bottom of diamond
                        // TileToScreen returns the top point, which is centered horizontally
                        // For 1024-wide texture, offset by -512 to get left edge
                        // For standard 1024x512 tiles: offset upward by TileHeight to align bottom
                        // For tiles with overdraw (taller than TileHeight), offset by overdraw amount
                        float overdrawHeight = texture.Height > IsometricMath.TileHeight 
                            ? (texture.Height - IsometricMath.TileHeight) 
                            : 0;
                        float totalOffset = IsometricMath.TileHeight + overdrawHeight;
                        
                        // Center horizontally: TileToScreen returns center, so offset by half width
                        float drawX = screenX - (texture.Width / 2.0f);
                        float drawY = screenY - totalOffset; // Move up to align bottom
                        
                        // Round to nearest pixel for perfect alignment
                        int finalDrawX = (int)Math.Round(drawX);
                        int finalDrawY = (int)Math.Round(drawY);
                        
                        g.DrawImage(
                            texture,
                            new Rectangle(finalDrawX, finalDrawY, texture.Width, texture.Height),
                            0, 0, texture.Width, texture.Height,
                            System.Drawing.GraphicsUnit.Pixel,
                            imageAttributes);
                    }
                }
            }
        }

        private RectangleF GetViewportBounds(Graphics g)
        {
            // Get the visible area in world coordinates
            // Convert screen bounds to world coordinates
            PointF topLeft = ScreenToWorld(new Point(0, 0));
            PointF bottomRight = ScreenToWorld(new Point(this.Width, this.Height));
            
            return new RectangleF(
                Math.Min(topLeft.X, bottomRight.X),
                Math.Min(topLeft.Y, bottomRight.Y),
                Math.Abs(bottomRight.X - topLeft.X),
                Math.Abs(bottomRight.Y - topLeft.Y)
            );
        }

        private void RebuildTileCache()
        {
            if (_mapData == null) return;
            
            _cachedTiles = new List<(int x, int y, TerrainType type)>();
            for (int x = 0; x < _mapData.Width; x++)
            {
                for (int y = 0; y < _mapData.Height; y++)
                {
                    var tile = _mapData.GetTile(x, y);
                    if (tile != null)
                    {
                        _cachedTiles.Add((x, y, tile.TerrainType));
                    }
                }
            }
            _tilesDirty = false;
        }

        private void RequestInvalidate()
        {
            if (!_pendingInvalidate)
            {
                _pendingInvalidate = true;
                // Use BeginInvoke to batch invalidate calls
                if (this.IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        Invalidate();
                        _pendingInvalidate = false;
                    }));
                }
                else
                {
                    Invalidate();
                    _pendingInvalidate = false;
                }
            }
        }

        private PointF ScreenToWorld(Point screenPoint)
        {
            // Apply inverse camera transform
            // Transform is: Translate(-pos) * Scale(zoom)
            // Inverse is: Scale(1/zoom) * Translate(pos)
            // So: world = (screen + pos) / zoom
            float worldX = (screenPoint.X + _camera.Position.X) / _camera.Zoom;
            float worldY = (screenPoint.Y + _camera.Position.Y) / _camera.Zoom;
            return new PointF(worldX, worldY);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None; // Changed to None for pixel-perfect alignment
            g.CompositingMode = CompositingMode.SourceOver; // Ensure proper alpha blending
            g.CompositingQuality = CompositingQuality.Default; // Standard quality for better performance (sufficient for pixel art)

            // Check if map data is initialized
            if (_mapData == null || _mapData.Width == 0 || _mapData.Height == 0)
            {
                // Draw loading message or just return (black screen is expected until loaded)
                using (Font font = new Font("Arial", 14))
                using (SolidBrush brush = new SolidBrush(Color.White))
                {
                    string message = "Loading map...";
                    SizeF textSize = g.MeasureString(message, font);
                    float x = (this.Width - textSize.Width) / 2;
                    float y = (this.Height - textSize.Height) / 2;
                    g.DrawString(message, font, brush, x, y);
                }
                return;
            }

            // Apply camera transform
            Matrix originalTransform = g.Transform;
            g.Transform = _camera.GetTransformMatrix();

            // Get cached tile list, rebuild if dirty
            if (_tilesDirty || _cachedTiles == null)
            {
                RebuildTileCache();
            }
            var tiles = _cachedTiles!;

            // Calculate viewport bounds for culling (approximate)
            RectangleF viewportBounds = GetViewportBounds(g);
            
            // Filter and sort visible tiles only
            var visibleSortedTiles = tiles.Where(t =>
            {
                var (screenX, screenY) = IsometricMath.TileToScreen(t.x, t.y);
                // Check if tile is roughly in viewport (with margin for tile size)
                float margin = IsometricMath.TileWidth;
                return screenX >= viewportBounds.Left - margin && 
                       screenX <= viewportBounds.Right + margin &&
                       screenY >= viewportBounds.Top - margin && 
                       screenY <= viewportBounds.Bottom + margin;
            }).OrderBy(t =>
            {
                var (screenX, screenY) = IsometricMath.TileToScreen(t.x, t.y);
                return screenY + screenX; // Sort by depth (Y + X for isometric)
            }).ToList();

            // Draw tiles
            foreach (var tile in visibleSortedTiles)
            {
                var (screenX, screenY) = IsometricMath.TileToScreen(tile.x, tile.y);
                Bitmap? texture = _textureLoader.GetTexture(tile.type);
                
                if (texture != null)
                {
                    // All tiles place at grid corner point
                    // screenX, screenY is the grid corner (top of isometric diamond)
                    // Draw tiles at their natural position - top of diamond at grid corner
                    
                    if (tile.type == TerrainType.Test || tile.type == TerrainType.Test2)
                    {
                        // Test tiles: 1024x1024, but bottom 1024x512 is the diamond, top 512 is overdraw
                        // Grid corner (screenX, screenY) should align with bottom of diamond
                        // TileToScreen returns the top point, which is centered horizontally
                        // Offset upward by TileHeight + overdraw to align bottom diamond with grid corner
                        float overdrawHeight = texture.Height - IsometricMath.TileHeight; // 512 for Test tile
                        float totalOffset = IsometricMath.TileHeight + overdrawHeight; // 512 + 512 = 1024
                        
                        // Center horizontally: TileToScreen returns center, so offset by half width
                        float drawX = screenX - (texture.Width / 2.0f);
                        float drawY = screenY - totalOffset; // Move up to align bottom diamond
                        
                        // Round to nearest pixel for perfect alignment
                        int finalDrawX = (int)Math.Round(drawX);
                        int finalDrawY = (int)Math.Round(drawY);
                        
                        // Draw with transparency support - always use the bitmap's natural alpha channel
                        // If opacity is less than 1.0, apply it via ImageAttributes
                        if (_tileOpacity >= 0.999f)
                        {
                            // Full opacity - draw directly to preserve transparency naturally
                            // The bitmap's alpha channel will be used for transparency
                            g.DrawImage(texture, finalDrawX, finalDrawY, texture.Width, texture.Height);
                        }
                        else
                        {
                            // Apply opacity using ImageAttributes
                            using (System.Drawing.Imaging.ImageAttributes imageAttributes = new System.Drawing.Imaging.ImageAttributes())
                            {
                                // ColorMatrix that multiplies existing alpha by opacity setting
                                // This preserves per-pixel transparency while applying the opacity slider
                                System.Drawing.Imaging.ColorMatrix colorMatrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
                                {
                                    new float[] {1, 0, 0, 0, 0},
                                    new float[] {0, 1, 0, 0, 0},
                                    new float[] {0, 0, 1, 0, 0},
                                    new float[] {0, 0, 0, _tileOpacity, 0}, // Multiply alpha channel by opacity
                                    new float[] {0, 0, 0, 0, 1}
                                });
                                imageAttributes.SetColorMatrix(colorMatrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
                                imageAttributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY); // Prevent edge artifacts
                                
                                g.DrawImage(
                                    texture,
                                    new Rectangle(finalDrawX, finalDrawY, texture.Width, texture.Height),
                                    0, 0, texture.Width, texture.Height,
                                    System.Drawing.GraphicsUnit.Pixel,
                                    imageAttributes);
                            }
                        }
                    }
                    else
                    {
                        // Regular tiles: align bottom of diamond with grid corner
                        // Grid corner (screenX, screenY) should align with bottom of diamond
                        // TileToScreen returns the top point, which is centered horizontally
                        // For 1024-wide texture, offset by -512 to get left edge
                        // For standard 1024x512 tiles: offset upward by TileHeight to align bottom
                        // For tiles with overdraw (taller than TileHeight), add overdraw offset
                        float overdrawHeight = texture.Height > IsometricMath.TileHeight 
                            ? (texture.Height - IsometricMath.TileHeight) 
                            : 0;
                        float totalOffset = IsometricMath.TileHeight + overdrawHeight;
                        
                        // Center horizontally: TileToScreen returns center, so offset by half width
                        float drawX = screenX - (texture.Width / 2.0f);
                        float drawY = screenY - totalOffset; // Move up to align bottom
                        
                        // Round to nearest pixel for perfect alignment
                        int finalDrawX = (int)Math.Round(drawX);
                        int finalDrawY = (int)Math.Round(drawY);
                        g.DrawImage(texture, finalDrawX, finalDrawY, texture.Width, texture.Height);
                    }
                }
            }

            // Draw hover preview (only if not in collision mode)
            if (!_collisionMode && _hoverTileX.HasValue && _hoverTileY.HasValue && !_isDragging)
            {
                DrawHoverPreview(g);
            }

            // Draw enemies
            for (int i = 0; i < _mapData.MapData.Enemies.Count; i++)
            {
                var enemy = _mapData.MapData.Enemies[i];
                DrawEnemy(g, enemy, enemy == _draggedEnemy, i);
            }

            // Draw player
            if (_mapData.MapData.Player != null)
            {
                DrawPlayer(g, _mapData.MapData.Player, _isDraggingPlayer);
            }
            else
            {
                // Debug: log if player is null
                Console.WriteLine("[MapRenderControl] Player is null, not drawing");
            }

            // Draw cameras
            for (int i = 0; i < _mapData.MapData.Cameras.Count; i++)
            {
                var camera = _mapData.MapData.Cameras[i];
                DrawCamera(g, camera, camera == _draggedCamera, i);
            }

            // Draw weapons
            if (_mapData.MapData.Weapons != null)
            {
                for (int i = 0; i < _mapData.MapData.Weapons.Count; i++)
                {
                    var weapon = _mapData.MapData.Weapons[i];
                    DrawWeapon(g, weapon, weapon == _draggedWeapon, i);
                }
            }

            // Draw grids from smallest to largest (so larger grids overlay smaller ones)
            // Highlight the grid that matches the current snap size
            if (_showGrid32x16)
            {
                DrawGrid32x16(g, _gridSnapWidth == 32.0f);
            }
            if (_showGrid64x32)
            {
                DrawGrid64x32(g, _gridSnapWidth == 64.0f);
            }
            if (_showGrid128x64)
            {
                DrawGrid128x64(g, _gridSnapWidth == 128.0f);
            }
            if (_showGrid512x256)
            {
                DrawGrid512x256(g, _gridSnapWidth == 512.0f);
            }
            if (_showGrid1024x512)
            {
                DrawGrid1024x512(g, _gridSnapWidth == 1024.0f);
            }

            // Draw collision cells
            DrawCollisionCells(g);
            
            // Draw collision hover preview (64x32 diamond cursor)
            if (_collisionMode && _collisionHoverPosition.HasValue && !_isDragging)
            {
                DrawCollisionHoverPreview(g, _collisionHoverPosition.Value);
            }

            // Restore original transform
            g.Transform = originalTransform;
        }
        
        /// <summary>
        /// Convert 3D world coordinates to 2D screen coordinates using isometric projection
        /// This matches the game's WorldToScreen3D method for proper isometric perspective
        /// </summary>
        private PointF WorldToScreen3D(float worldX, float worldY, float zHeight, float heightScale = 0.5f)
        {
            // Isometric projection: same as game's Entity.WorldToScreen3D
            // This converts world coordinates to isometric screen coordinates
            float screenX = (worldX - worldY) * (Project9.Shared.IsometricMath.TileWidth / 2.0f);
            float screenY = (worldX + worldY) * (Project9.Shared.IsometricMath.TileHeight / 2.0f) - zHeight * heightScale;
            return new PointF(screenX, screenY);
        }
        
        /// <summary>
        /// Draw a 3D isometric bounding box wireframe
        /// Entities are drawn at their world X, Y coordinates directly
        /// We use isometric projection to convert 3D bounding box to screen space
        /// </summary>
        private void DrawBoundingBox3D(Graphics g, float centerX, float centerY, float zHeight, float width, float height, float depth, Color boxColor)
        {
            float halfWidth = width / 2.0f;
            float halfHeight = height / 2.0f;
            
            // Calculate opacity for filled faces (used in both 2D and 3D cases)
            int alpha = (int)(_boundingBoxOpacity * 255.0f);
            Color fillColor = Color.FromArgb(alpha, boxColor);
            
            // ZHeight represents the TOP of the object
            // Base is always at z = 0, top is at z = zHeight
            // We draw in world coordinates (Graphics has camera transform applied)
            
            // If zHeight is 0 or less, just draw the base diamond
            if (zHeight <= 0)
            {
                // Draw a simple diamond outline at the base
                PointF[] diamondPoints = new PointF[]
                {
                    new PointF(centerX, centerY - halfHeight),
                    new PointF(centerX + halfWidth, centerY),
                    new PointF(centerX, centerY + halfHeight),
                    new PointF(centerX - halfWidth, centerY)
                };
                
                // Draw filled diamond with semi-transparent cyan
                using (SolidBrush fillBrush = new SolidBrush(fillColor))
                {
                    g.FillPolygon(fillBrush, diamondPoints);
                }
                
                // Draw outline
                using (Pen boxPen = new Pen(boxColor, 2.0f))
                {
                    g.DrawPolygon(boxPen, diamondPoints);
                }
                return;
            }
            
            // For 3D bounding box, we use the same isometric diamond shape as the entity
            // The Graphics object has a camera transform, so we draw in world coordinates
            // For isometric Z projection, we adjust the Y coordinate based on Z height
            // Isometric Z projection: screenY = worldY - zHeight * heightScale
            // Since we're in world space, we adjust the world Y coordinate
            const float heightScale = 0.5f;
            float zOffsetY = zHeight * heightScale;
            
            // Bottom face corners (z = 0) - base of the object (isometric diamond)
            PointF bottomTop = new PointF(centerX, centerY - halfHeight);
            PointF bottomRight = new PointF(centerX + halfWidth, centerY);
            PointF bottomBottom = new PointF(centerX, centerY + halfHeight);
            PointF bottomLeft = new PointF(centerX - halfWidth, centerY);
            
            // Top face corners (z = zHeight) - top of the object
            // In isometric, Z height affects the Y coordinate (moves up in screen space)
            PointF topTop = new PointF(centerX, centerY - halfHeight - zOffsetY);
            PointF topRight = new PointF(centerX + halfWidth, centerY - zOffsetY);
            PointF topBottom = new PointF(centerX, centerY + halfHeight - zOffsetY);
            PointF topLeft = new PointF(centerX - halfWidth, centerY - zOffsetY);
            
            // Draw filled faces with semi-transparent cyan
            using (SolidBrush fillBrush = new SolidBrush(fillColor))
            {
                // Bottom face (isometric diamond at z=0)
                PointF[] bottomFace = new PointF[] { bottomTop, bottomRight, bottomBottom, bottomLeft };
                g.FillPolygon(fillBrush, bottomFace);
                
                // Top face (isometric diamond at z=zHeight)
                PointF[] topFace = new PointF[] { topTop, topRight, topBottom, topLeft };
                g.FillPolygon(fillBrush, topFace);
                
                // Draw side faces (4 trapezoids connecting bottom to top)
                PointF[] sideFace1 = new PointF[] { bottomTop, bottomRight, topRight, topTop };
                g.FillPolygon(fillBrush, sideFace1);
                
                PointF[] sideFace2 = new PointF[] { bottomRight, bottomBottom, topBottom, topRight };
                g.FillPolygon(fillBrush, sideFace2);
                
                PointF[] sideFace3 = new PointF[] { bottomBottom, bottomLeft, topLeft, topBottom };
                g.FillPolygon(fillBrush, sideFace3);
                
                PointF[] sideFace4 = new PointF[] { bottomLeft, bottomTop, topTop, topLeft };
                g.FillPolygon(fillBrush, sideFace4);
            }
            
            // Draw wireframe outline using entity's bounding box color with thicker lines
            using (Pen boxPen = new Pen(boxColor, 3.0f))
            {
                // Bottom face (isometric diamond at z=0)
                g.DrawLine(boxPen, bottomTop, bottomRight);
                g.DrawLine(boxPen, bottomRight, bottomBottom);
                g.DrawLine(boxPen, bottomBottom, bottomLeft);
                g.DrawLine(boxPen, bottomLeft, bottomTop);
                
                // Top face (isometric diamond at z=zHeight)
                g.DrawLine(boxPen, topTop, topRight);
                g.DrawLine(boxPen, topRight, topBottom);
                g.DrawLine(boxPen, topBottom, topLeft);
                g.DrawLine(boxPen, topLeft, topTop);
                
                // Vertical edges connecting bottom to top
                g.DrawLine(boxPen, bottomTop, topTop);
                g.DrawLine(boxPen, bottomRight, topRight);
                g.DrawLine(boxPen, bottomBottom, topBottom);
                g.DrawLine(boxPen, bottomLeft, topLeft);
            }
        }

        private void DrawCollisionCells(Graphics g)
        {
            const float halfWidth = 32.0f;  // 64/2 = 32
            const float halfHeight = 16.0f; // 32/2 = 16

            foreach (var cell in _collisionCells)
            {
                float centerX = cell.X;
                float centerY = cell.Y;

                // Define the 4 points of the isometric diamond (64x32 size)
                PointF[] diamondPoints = new PointF[]
                {
                    new PointF(centerX, centerY - halfHeight),                    // Top
                    new PointF(centerX + halfWidth, centerY),                     // Right
                    new PointF(centerX, centerY + halfHeight),                    // Bottom
                    new PointF(centerX - halfWidth, centerY)                      // Left
                };

                // Draw filled isometric diamond in purple
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(180, 128, 0, 128))) // Purple with transparency
                {
                    g.FillPolygon(brush, diamondPoints);
                }

                // Draw outline
                using (Pen pen = new Pen(Color.Purple, 2))
                {
                    g.DrawPolygon(pen, diamondPoints);
                }
            }
        }

        private void DrawCollisionHoverPreview(Graphics g, PointF position)
        {
            const float halfWidth = 32.0f;  // 64/2 = 32
            const float halfHeight = 16.0f; // 32/2 = 16

            float centerX = position.X;
            float centerY = position.Y;

            // Define the 4 points of the isometric diamond (64x32 size)
            PointF[] diamondPoints = new PointF[]
            {
                new PointF(centerX, centerY - halfHeight),                    // Top
                new PointF(centerX + halfWidth, centerY),                     // Right
                new PointF(centerX, centerY + halfHeight),                    // Bottom
                new PointF(centerX - halfWidth, centerY)                      // Left
            };

            // Check if there's already a collision cell at this position
            bool cellExists = _collisionCells.Any(c => 
                Math.Abs(c.X - centerX) < 1 && Math.Abs(c.Y - centerY) < 1);

            // Draw filled isometric diamond preview (lighter/more transparent for hover)
            if (cellExists)
            {
                // Red tint if cell already exists (will be deleted on click)
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(150, 255, 100, 100))) // Light red with transparency
                {
                    g.FillPolygon(brush, diamondPoints);
                }
                using (Pen pen = new Pen(Color.Red, 2))
                {
                    g.DrawPolygon(pen, diamondPoints);
                }
            }
            else
            {
                // Light purple for placement preview
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(150, 200, 100, 200))) // Light purple with transparency
                {
                    g.FillPolygon(brush, diamondPoints);
                }
                using (Pen pen = new Pen(Color.Purple, 2))
                {
                    g.DrawPolygon(pen, diamondPoints);
                }
            }
        }

        private void DrawGrid32x16(Graphics g, bool isSelected = false)
        {
            const float gridX = 32.0f;
            
            // Use a lighter, more subtle color for the finer grid (semi-transparent light gray)
            // Highlight if this is the selected grid
            Color gridColor = isSelected 
                ? Color.FromArgb(180, 150, 200, 255) // Brighter blue when selected
                : Color.FromArgb(60, 100, 100, 100); // Normal light gray
            int lineWidth = isSelected ? 2 : 1;
            
            using (Pen gridPen = new Pen(gridColor, lineWidth))
            {
                // Calculate visible area in world coordinates (already transformed by camera)
                PointF topLeft = ScreenToWorld(new Point(0, 0));
                PointF bottomRight = ScreenToWorld(new Point(this.Width, this.Height));
                
                // Expand bounds to ensure we draw enough grid lines
                float minX = topLeft.X - IsometricMath.TileWidth * 2;
                float maxX = bottomRight.X + IsometricMath.TileWidth * 2;
                float minY = topLeft.Y - IsometricMath.TileHeight * 2;
                float maxY = bottomRight.Y + IsometricMath.TileHeight * 2;
                
                // Convert visible area to tile coordinates to find which tiles are visible
                var (minTileX, minTileY) = IsometricMath.ScreenToTile(minX, minY);
                var (maxTileX, maxTileY) = IsometricMath.ScreenToTile(maxX, maxY);
                
                // Expand tile range
                minTileX -= 3;
                minTileY -= 3;
                maxTileX += 3;
                maxTileY += 3;
                
                // Grid cells per tile: 1024/32 = 32 cells horizontally, 512/16 = 32 cells vertically
                const int gridCellsPerTile = (int)(IsometricMath.TileWidth / gridX);
                
                // Draw lines parallel to tile edges (isometric lines)
                // Lines going northeast-southwest (parallel to tile top/bottom edges)
                for (int tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                    {
                        // Calculate offset within the tile
                        float cellProgress = gridCell / (float)gridCellsPerTile;
                        float offsetX = cellProgress * (IsometricMath.TileWidth / 2.0f);
                        float offsetY = cellProgress * (IsometricMath.TileHeight / 2.0f);
                        
                        // Draw line from bottom to top of visible area
                        // Start from bottom of visible tiles
                        var (startX, startY) = IsometricMath.TileToScreen(tileX, minTileY);
                        startX += offsetX;
                        startY += offsetY;
                        
                        // End at top of visible tiles
                        var (endX, endY) = IsometricMath.TileToScreen(tileX, maxTileY);
                        endX += offsetX;
                        endY += offsetY;
                        
                        // Clip to visible bounds
                        if ((startY >= minY && startY <= maxY) || (endY >= minY && endY <= maxY) ||
                            (startY < minY && endY > maxY) || (startY > maxY && endY < minY))
                        {
                            g.DrawLine(gridPen, startX, startY, endX, endY);
                        }
                    }
                }
                
                // Lines going northwest-southeast (parallel to tile left/right edges)
                for (int tileY = minTileY; tileY <= maxTileY; tileY++)
                {
                    for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                    {
                        // Calculate offset within the tile (negative X, positive Y)
                        float cellProgress = gridCell / (float)gridCellsPerTile;
                        float offsetX = -cellProgress * (IsometricMath.TileWidth / 2.0f);
                        float offsetY = cellProgress * (IsometricMath.TileHeight / 2.0f);
                        
                        // Draw line from left to right of visible area
                        var (startX, startY) = IsometricMath.TileToScreen(minTileX, tileY);
                        startX += offsetX;
                        startY += offsetY;
                        
                        var (endX, endY) = IsometricMath.TileToScreen(maxTileX, tileY);
                        endX += offsetX;
                        endY += offsetY;
                        
                        // Clip to visible bounds
                        if ((startX >= minX && startX <= maxX) || (endX >= minX && endX <= maxX) ||
                            (startX < minX && endX > maxX) || (startX > maxX && endX < minX))
                        {
                            g.DrawLine(gridPen, startX, startY, endX, endY);
                        }
                    }
                }
            }
        }

        private void DrawGrid128x64(Graphics g, bool isSelected = false)
        {
            const float gridX = 128.0f;
            
            // Use a medium color for the 128x64 grid
            // Highlight if this is the selected grid
            Color gridColor = isSelected 
                ? Color.FromArgb(200, 150, 200, 255) // Brighter blue when selected
                : Color.FromArgb(120, 100, 100, 100); // Normal medium gray
            int lineWidth = isSelected ? 2 : 1;
            
            using (Pen gridPen = new Pen(gridColor, lineWidth))
            {
                // Calculate visible area in world coordinates (already transformed by camera)
                PointF topLeft = ScreenToWorld(new Point(0, 0));
                PointF bottomRight = ScreenToWorld(new Point(this.Width, this.Height));
                
                // Expand bounds to ensure we draw enough grid lines
                float minX = topLeft.X - IsometricMath.TileWidth * 2;
                float maxX = bottomRight.X + IsometricMath.TileWidth * 2;
                float minY = topLeft.Y - IsometricMath.TileHeight * 2;
                float maxY = bottomRight.Y + IsometricMath.TileHeight * 2;
                
                // Convert visible area to tile coordinates to find which tiles are visible
                var (minTileX, minTileY) = IsometricMath.ScreenToTile(minX, minY);
                var (maxTileX, maxTileY) = IsometricMath.ScreenToTile(maxX, maxY);
                
                // Expand tile range
                minTileX -= 3;
                minTileY -= 3;
                maxTileX += 3;
                maxTileY += 3;
                
                // Grid cells per tile
                const int gridCellsPerTile = (int)(IsometricMath.TileWidth / gridX);
                
                // Draw lines parallel to tile edges (isometric lines)
                // Lines going northeast-southwest (parallel to tile top/bottom edges)
                for (int tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                    {
                        // Calculate offset within the tile
                        float cellProgress = gridCell / (float)gridCellsPerTile;
                        float offsetX = cellProgress * (IsometricMath.TileWidth / 2.0f);
                        float offsetY = cellProgress * (IsometricMath.TileHeight / 2.0f);
                        
                        // Draw line from bottom to top of visible area
                        var (startX, startY) = IsometricMath.TileToScreen(tileX, minTileY);
                        startX += offsetX;
                        startY += offsetY;
                        
                        var (endX, endY) = IsometricMath.TileToScreen(tileX, maxTileY);
                        endX += offsetX;
                        endY += offsetY;
                        
                        // Clip to visible bounds
                        if ((startY >= minY && startY <= maxY) || (endY >= minY && endY <= maxY) ||
                            (startY < minY && endY > maxY) || (startY > maxY && endY < minY))
                        {
                            g.DrawLine(gridPen, startX, startY, endX, endY);
                        }
                    }
                }
                
                // Lines going northwest-southeast (parallel to tile left/right edges)
                for (int tileY = minTileY; tileY <= maxTileY; tileY++)
                {
                    for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                    {
                        // Calculate offset within the tile (negative X, positive Y)
                        float cellProgress = gridCell / (float)gridCellsPerTile;
                        float offsetX = -cellProgress * (IsometricMath.TileWidth / 2.0f);
                        float offsetY = cellProgress * (IsometricMath.TileHeight / 2.0f);
                        
                        // Draw line from left to right of visible area
                        var (startX, startY) = IsometricMath.TileToScreen(minTileX, tileY);
                        startX += offsetX;
                        startY += offsetY;
                        
                        var (endX, endY) = IsometricMath.TileToScreen(maxTileX, tileY);
                        endX += offsetX;
                        endY += offsetY;
                        
                        // Clip to visible bounds
                        if ((startX >= minX && startX <= maxX) || (endX >= minX && endX <= maxX) ||
                            (startX < minX && endX > maxX) || (startX > maxX && endX < minX))
                        {
                            g.DrawLine(gridPen, startX, startY, endX, endY);
                        }
                    }
                }
            }
        }

        private void DrawGrid512x256(Graphics g, bool isSelected = false)
        {
            const float gridX = 512.0f;
            
            // Use a darker color for the 512x256 grid
            // Highlight if this is the selected grid
            Color gridColor = isSelected 
                ? Color.FromArgb(220, 150, 200, 255) // Brighter blue when selected
                : Color.FromArgb(150, 120, 120, 120); // Normal dark gray
            int lineWidth = isSelected ? 3 : 2;
            
            using (Pen gridPen = new Pen(gridColor, lineWidth))
            {
                // Calculate visible area in world coordinates (already transformed by camera)
                PointF topLeft = ScreenToWorld(new Point(0, 0));
                PointF bottomRight = ScreenToWorld(new Point(this.Width, this.Height));
                
                // Expand bounds to ensure we draw enough grid lines
                float minX = topLeft.X - IsometricMath.TileWidth * 2;
                float maxX = bottomRight.X + IsometricMath.TileWidth * 2;
                float minY = topLeft.Y - IsometricMath.TileHeight * 2;
                float maxY = bottomRight.Y + IsometricMath.TileHeight * 2;
                
                // Convert visible area to tile coordinates to find which tiles are visible
                var (minTileX, minTileY) = IsometricMath.ScreenToTile(minX, minY);
                var (maxTileX, maxTileY) = IsometricMath.ScreenToTile(maxX, maxY);
                
                // Expand tile range
                minTileX -= 3;
                minTileY -= 3;
                maxTileX += 3;
                maxTileY += 3;
                
                // Grid cells per tile
                const int gridCellsPerTile = (int)(IsometricMath.TileWidth / gridX);
                
                // Draw lines parallel to tile edges (isometric lines)
                // Lines going northeast-southwest (parallel to tile top/bottom edges)
                for (int tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                    {
                        // Calculate offset within the tile
                        float cellProgress = gridCell / (float)gridCellsPerTile;
                        float offsetX = cellProgress * (IsometricMath.TileWidth / 2.0f);
                        float offsetY = cellProgress * (IsometricMath.TileHeight / 2.0f);
                        
                        // Draw line from bottom to top of visible area
                        var (startX, startY) = IsometricMath.TileToScreen(tileX, minTileY);
                        startX += offsetX;
                        startY += offsetY;
                        
                        var (endX, endY) = IsometricMath.TileToScreen(tileX, maxTileY);
                        endX += offsetX;
                        endY += offsetY;
                        
                        // Clip to visible bounds
                        if ((startY >= minY && startY <= maxY) || (endY >= minY && endY <= maxY) ||
                            (startY < minY && endY > maxY) || (startY > maxY && endY < minY))
                        {
                            g.DrawLine(gridPen, startX, startY, endX, endY);
                        }
                    }
                }
                
                // Lines going northwest-southeast (parallel to tile left/right edges)
                for (int tileY = minTileY; tileY <= maxTileY; tileY++)
                {
                    for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                    {
                        // Calculate offset within the tile (negative X, positive Y)
                        float cellProgress = gridCell / (float)gridCellsPerTile;
                        float offsetX = -cellProgress * (IsometricMath.TileWidth / 2.0f);
                        float offsetY = cellProgress * (IsometricMath.TileHeight / 2.0f);
                        
                        // Draw line from left to right of visible area
                        var (startX, startY) = IsometricMath.TileToScreen(minTileX, tileY);
                        startX += offsetX;
                        startY += offsetY;
                        
                        var (endX, endY) = IsometricMath.TileToScreen(maxTileX, tileY);
                        endX += offsetX;
                        endY += offsetY;
                        
                        // Clip to visible bounds
                        if ((startX >= minX && startX <= maxX) || (endX >= minX && endX <= maxX) ||
                            (startX < minX && endX > maxX) || (startX > maxX && endX < minX))
                        {
                            g.DrawLine(gridPen, startX, startY, endX, endY);
                        }
                    }
                }
            }
        }

        private void DrawGrid1024x512(Graphics g, bool isSelected = false)
        {
            const float gridX = 1024.0f;
            
            // Use a darker, thicker line for the 1024x512 grid (tile boundaries)
            // Highlight if this is the selected grid
            Color gridColor = isSelected 
                ? Color.FromArgb(240, 150, 200, 255) // Brighter blue when selected
                : Color.FromArgb(200, 150, 150, 150); // Normal dark gray
            int lineWidth = isSelected ? 4 : 3;
            
            using (Pen gridPen = new Pen(gridColor, lineWidth))
            {
                // Calculate visible area in world coordinates (already transformed by camera)
                PointF topLeft = ScreenToWorld(new Point(0, 0));
                PointF bottomRight = ScreenToWorld(new Point(this.Width, this.Height));
                
                // Expand bounds to ensure we draw enough grid lines
                float minX = topLeft.X - IsometricMath.TileWidth * 2;
                float maxX = bottomRight.X + IsometricMath.TileWidth * 2;
                float minY = topLeft.Y - IsometricMath.TileHeight * 2;
                float maxY = bottomRight.Y + IsometricMath.TileHeight * 2;
                
                // Convert visible area to tile coordinates to find which tiles are visible
                var (minTileX, minTileY) = IsometricMath.ScreenToTile(minX, minY);
                var (maxTileX, maxTileY) = IsometricMath.ScreenToTile(maxX, maxY);
                
                // Expand tile range
                minTileX -= 3;
                minTileY -= 3;
                maxTileX += 3;
                maxTileY += 3;
                
                // Grid cells per tile (1024/1024 = 1, so this is just tile boundaries)
                const int gridCellsPerTile = (int)(IsometricMath.TileWidth / gridX);
                
                // Draw lines parallel to tile edges (isometric lines)
                // Lines going northeast-southwest (parallel to tile top/bottom edges)
                for (int tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                    {
                        // Calculate offset within the tile
                        float cellProgress = gridCell / (float)gridCellsPerTile;
                        float offsetX = cellProgress * (IsometricMath.TileWidth / 2.0f);
                        float offsetY = cellProgress * (IsometricMath.TileHeight / 2.0f);
                        
                        // Draw line from bottom to top of visible area
                        var (startX, startY) = IsometricMath.TileToScreen(tileX, minTileY);
                        startX += offsetX;
                        startY += offsetY;
                        
                        var (endX, endY) = IsometricMath.TileToScreen(tileX, maxTileY);
                        endX += offsetX;
                        endY += offsetY;
                        
                        // Clip to visible bounds
                        if ((startY >= minY && startY <= maxY) || (endY >= minY && endY <= maxY) ||
                            (startY < minY && endY > maxY) || (startY > maxY && endY < minY))
                        {
                            g.DrawLine(gridPen, startX, startY, endX, endY);
                        }
                    }
                }
                
                // Lines going northwest-southeast (parallel to tile left/right edges)
                for (int tileY = minTileY; tileY <= maxTileY; tileY++)
                {
                    for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                    {
                        // Calculate offset within the tile (negative X, positive Y)
                        float cellProgress = gridCell / (float)gridCellsPerTile;
                        float offsetX = -cellProgress * (IsometricMath.TileWidth / 2.0f);
                        float offsetY = cellProgress * (IsometricMath.TileHeight / 2.0f);
                        
                        // Draw line from left to right of visible area
                        var (startX, startY) = IsometricMath.TileToScreen(minTileX, tileY);
                        startX += offsetX;
                        startY += offsetY;
                        
                        var (endX, endY) = IsometricMath.TileToScreen(maxTileX, tileY);
                        endX += offsetX;
                        endY += offsetY;
                        
                        // Clip to visible bounds
                        if ((startX >= minX && startX <= maxX) || (endX >= minX && endX <= maxX) ||
                            (startX < minX && endX > maxX) || (startX > maxX && endX < minX))
                        {
                            g.DrawLine(gridPen, startX, startY, endX, endY);
                        }
                    }
                }
            }
        }

        private void DrawGrid64x32(Graphics g, bool isSelected = false)
        {
            const float gridX = 64.0f;
            
            // Use a darker, less intense color (semi-transparent dark gray)
            // Highlight if this is the selected grid
            Color gridColor = isSelected 
                ? Color.FromArgb(180, 150, 200, 255) // Brighter blue when selected
                : Color.FromArgb(100, 80, 80, 80); // Normal dark gray
            int lineWidth = isSelected ? 2 : 1;
            
            using (Pen gridPen = new Pen(gridColor, lineWidth))
            {
                // Calculate visible area in world coordinates (already transformed by camera)
                PointF topLeft = ScreenToWorld(new Point(0, 0));
                PointF bottomRight = ScreenToWorld(new Point(this.Width, this.Height));
                
                // Expand bounds to ensure we draw enough grid lines
                float minX = topLeft.X - IsometricMath.TileWidth * 2;
                float maxX = bottomRight.X + IsometricMath.TileWidth * 2;
                float minY = topLeft.Y - IsometricMath.TileHeight * 2;
                float maxY = bottomRight.Y + IsometricMath.TileHeight * 2;
                
                // Convert visible area to tile coordinates to find which tiles are visible
                var (minTileX, minTileY) = IsometricMath.ScreenToTile(minX, minY);
                var (maxTileX, maxTileY) = IsometricMath.ScreenToTile(maxX, maxY);
                
                // Expand tile range
                minTileX -= 3;
                minTileY -= 3;
                maxTileX += 3;
                maxTileY += 3;
                
                // Grid cells per tile: 1024/64 = 16 cells horizontally, 512/32 = 16 cells vertically
                const int gridCellsPerTile = (int)(IsometricMath.TileWidth / gridX);
                
                // Draw lines parallel to tile edges (isometric lines)
                // Lines going northeast-southwest (parallel to tile top/bottom edges)
                for (int tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                    {
                        // Calculate offset within the tile
                        float cellProgress = gridCell / (float)gridCellsPerTile;
                        float offsetX = cellProgress * (IsometricMath.TileWidth / 2.0f);
                        float offsetY = cellProgress * (IsometricMath.TileHeight / 2.0f);
                        
                        // Draw line from bottom to top of visible area
                        // Start from bottom of visible tiles
                        var (startX, startY) = IsometricMath.TileToScreen(tileX, minTileY);
                        startX += offsetX;
                        startY += offsetY;
                        
                        // End at top of visible tiles
                        var (endX, endY) = IsometricMath.TileToScreen(tileX, maxTileY);
                        endX += offsetX;
                        endY += offsetY;
                        
                        // Clip to visible bounds
                        if ((startY >= minY && startY <= maxY) || (endY >= minY && endY <= maxY) ||
                            (startY < minY && endY > maxY) || (startY > maxY && endY < minY))
                        {
                            g.DrawLine(gridPen, startX, startY, endX, endY);
                        }
                    }
                }
                
                // Lines going northwest-southeast (parallel to tile left/right edges)
                for (int tileY = minTileY; tileY <= maxTileY; tileY++)
                {
                    for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                    {
                        // Calculate offset within the tile (negative X, positive Y)
                        float cellProgress = gridCell / (float)gridCellsPerTile;
                        float offsetX = -cellProgress * (IsometricMath.TileWidth / 2.0f);
                        float offsetY = cellProgress * (IsometricMath.TileHeight / 2.0f);
                        
                        // Draw line from left to right of visible area
                        var (startX, startY) = IsometricMath.TileToScreen(minTileX, tileY);
                        startX += offsetX;
                        startY += offsetY;
                        
                        var (endX, endY) = IsometricMath.TileToScreen(maxTileX, tileY);
                        endX += offsetX;
                        endY += offsetY;
                        
                        // Clip to visible bounds
                        if ((startX >= minX && startX <= maxX) || (endX >= minX && endX <= maxX) ||
                            (startX < minX && endX > maxX) || (startX > maxX && endX < minX))
                        {
                            g.DrawLine(gridPen, startX, startY, endX, endY);
                        }
                    }
                }
            }
        }

        private void DrawEnemy(Graphics g, EnemyData enemy, bool isDragging, int index)
        {
            float centerX = enemy.X;
            float centerY = enemy.Y;
            
            // Use diamond dimensions from enemy data
            float halfWidth = enemy.DiamondWidth / 2.0f;
            float halfHeight = enemy.DiamondHeight / 2.0f;
            
            // Define the 4 points of the isometric diamond
            PointF[] diamondPoints = new PointF[]
            {
                new PointF(centerX, centerY - halfHeight),                    // Top
                new PointF(centerX + halfWidth, centerY),                     // Right
                new PointF(centerX, centerY + halfHeight),                    // Bottom
                new PointF(centerX - halfWidth, centerY)                      // Left
            };
            
            // Get bounding box color from enemy data
            Color boxColor = Color.FromArgb(enemy.BoundingBoxColorR, enemy.BoundingBoxColorG, enemy.BoundingBoxColorB);
            
            // Draw filled isometric diamond - use bounding box color (or orange when dragging)
            using (SolidBrush brush = new SolidBrush(isDragging ? Color.Orange : boxColor))
            {
                g.FillPolygon(brush, diamondPoints);
            }
            
            // Draw outline
            using (Pen pen = new Pen(Color.White, 2))
            {
                g.DrawPolygon(pen, diamondPoints);
            }
            
            // Draw bounding box if enabled
            if (_showBoundingBoxes)
            {
                DrawBoundingBox3D(g, centerX, centerY, enemy.ZHeight, enemy.DiamondWidth, enemy.DiamondHeight, 64.0f, boxColor);
            }
            
            // Draw sight cone preview - only if ShowEnemyCones is enabled
            if (_showEnemyCones)
            {
                // Calculate sight cone length (use SightConeLength if set, otherwise DetectionRange * 0.8)
                float coneLength = enemy.SightConeLength > 0 
                    ? enemy.SightConeLength 
                    : enemy.DetectionRange * 0.8f;
                
                float coneAngleRad = enemy.Rotation;
                float halfAngleRad = (enemy.SightConeAngle * MathF.PI / 180.0f) / 2.0f;
                
                // Calculate cone edges
                float leftAngle = coneAngleRad - halfAngleRad;
                float rightAngle = coneAngleRad + halfAngleRad;
                
                float leftEndX = centerX + (float)Math.Cos(leftAngle) * coneLength;
                float leftEndY = centerY + (float)Math.Sin(leftAngle) * coneLength;
                float rightEndX = centerX + (float)Math.Cos(rightAngle) * coneLength;
                float rightEndY = centerY + (float)Math.Sin(rightAngle) * coneLength;
                
                // Draw sight cone as a filled triangle
                PointF[] conePoints = new PointF[]
                {
                    new PointF(centerX, centerY),
                    new PointF(leftEndX, leftEndY),
                    new PointF(rightEndX, rightEndY)
                };
                
                using (SolidBrush coneBrush = new SolidBrush(Color.FromArgb(80, Color.Yellow)))
                {
                    g.FillPolygon(coneBrush, conePoints);
                }
                
                using (Pen conePen = new Pen(Color.FromArgb(150, Color.Yellow), 2))
                {
                    g.DrawLine(conePen, centerX, centerY, leftEndX, leftEndY);
                    g.DrawLine(conePen, centerX, centerY, rightEndX, rightEndY);
                    g.DrawLine(conePen, leftEndX, leftEndY, rightEndX, rightEndY);
                }
            }
            
            // Draw label above the enemy - use name from JSON if available, otherwise use index
            string label;
            if (!string.IsNullOrWhiteSpace(enemy.Name))
            {
                label = enemy.Name;
            }
            else
            {
                label = $"Enemy {index}";
            }
            using (Font font = new Font("Arial", 10, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (SolidBrush backgroundBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
            {
                SizeF textSize = g.MeasureString(label, font);
                
                // Position label above the enemy (at the top of the diamond)
                float labelX = centerX - textSize.Width / 2.0f;
                float labelY = centerY - halfHeight - textSize.Height - 4; // 4 pixels above the diamond
                
                // Draw background rectangle for better visibility
                RectangleF backgroundRect = new RectangleF(
                    labelX - 2,
                    labelY - 1,
                    textSize.Width + 4,
                    textSize.Height + 2
                );
                g.FillRectangle(backgroundBrush, backgroundRect);
                
                // Draw text
                g.DrawString(label, font, textBrush, labelX, labelY);
            }
        }

        private void DrawPlayer(Graphics g, PlayerData player, bool isDragging)
        {
            float centerX = player.X;
            float centerY = player.Y;
            
            // Use diamond dimensions from player data
            float halfWidth = player.DiamondWidth / 2.0f;
            float halfHeight = player.DiamondHeight / 2.0f;
            
            // Define the 4 points of the isometric diamond
            PointF[] diamondPoints = new PointF[]
            {
                new PointF(centerX, centerY - halfHeight),                    // Top
                new PointF(centerX + halfWidth, centerY),                     // Right
                new PointF(centerX, centerY + halfHeight),                    // Bottom
                new PointF(centerX - halfWidth, centerY)                      // Left
            };
            
            // Get bounding box color from player data
            Color boxColor = Color.FromArgb(player.BoundingBoxColorR, player.BoundingBoxColorG, player.BoundingBoxColorB);
            
            // Draw filled isometric diamond - use bounding box color (or yellow when dragging)
            using (SolidBrush brush = new SolidBrush(isDragging ? Color.Yellow : boxColor))
            {
                g.FillPolygon(brush, diamondPoints);
            }
            
            // Draw outline
            using (Pen pen = new Pen(Color.White, 2))
            {
                g.DrawPolygon(pen, diamondPoints);
            }
            
            // Draw bounding box if enabled
            if (_showBoundingBoxes)
            {
                DrawBoundingBox3D(g, centerX, centerY, player.ZHeight, player.DiamondWidth, player.DiamondHeight, 64.0f, boxColor);
            }
            
            // Draw label above the player - use name from JSON if available
            string label;
            if (!string.IsNullOrWhiteSpace(player.Name))
            {
                label = player.Name;
            }
            else
            {
                label = "Player";
            }
            using (Font font = new Font("Arial", 10, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (SolidBrush backgroundBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
            {
                SizeF textSize = g.MeasureString(label, font);
                
                // Position label above the player (at the top of the diamond)
                float labelX = centerX - textSize.Width / 2.0f;
                float labelY = centerY - halfHeight - textSize.Height - 4; // 4 pixels above the diamond
                
                // Draw background rectangle for better visibility
                RectangleF backgroundRect = new RectangleF(
                    labelX - 2,
                    labelY - 1,
                    textSize.Width + 4,
                    textSize.Height + 2
                );
                g.FillRectangle(backgroundBrush, backgroundRect);
                
                // Draw text
                g.DrawString(label, font, textBrush, labelX, labelY);
            }
        }

        private void DrawWeapon(Graphics g, WeaponData weapon, bool isDragging, int index)
        {
            float centerX = weapon.X;
            float centerY = weapon.Y;
            
            // Determine weapon type and color based on data type
            bool isGun = weapon is GunData;
            Color weaponColor = isDragging 
                ? (isGun ? Color.Orange : Color.Yellow) 
                : (isGun ? Color.DarkGray : Color.Silver);
            
            // Isometric diamond dimensions (smaller than entities)
            float halfWidth = 12.0f;  // Half width of the isometric box (24 total)
            float halfHeight = 6.0f;  // Half height of the isometric box (12 total)
            
            // Define the 4 points of the isometric diamond
            PointF[] diamondPoints = new PointF[]
            {
                new PointF(centerX, centerY - halfHeight),                    // Top
                new PointF(centerX + halfWidth, centerY),                     // Right
                new PointF(centerX, centerY + halfHeight),                    // Bottom
                new PointF(centerX - halfWidth, centerY)                      // Left
            };
            
            // Draw filled isometric diamond with weapon-specific color
            using (SolidBrush brush = new SolidBrush(weaponColor))
            {
                g.FillPolygon(brush, diamondPoints);
            }
            
            // Draw outline
            using (Pen pen = new Pen(Color.White, 1))
            {
                g.DrawPolygon(pen, diamondPoints);
            }
            
            // Draw bounding box if enabled
            if (_showBoundingBoxes)
            {
                Color boxColor = Color.FromArgb(weapon.BoundingBoxColorR, weapon.BoundingBoxColorG, weapon.BoundingBoxColorB);
                DrawBoundingBox3D(g, centerX, centerY, weapon.ZHeight, 24.0f, 12.0f, 24.0f, boxColor);
            }
            
            // Draw label above the weapon
            string label = $"{weapon.Type} {index}";
            using (Font font = new Font("Arial", 9, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (SolidBrush backgroundBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
            {
                SizeF textSize = g.MeasureString(label, font);
                float labelX = centerX - textSize.Width / 2;
                float labelY = centerY - halfHeight - textSize.Height - 5;
                
                // Draw background rectangle
                RectangleF backgroundRect = new RectangleF(
                    labelX - 2,
                    labelY - 1,
                    textSize.Width + 4,
                    textSize.Height + 2
                );
                g.FillRectangle(backgroundBrush, backgroundRect);
                
                // Draw text
                g.DrawString(label, font, textBrush, labelX, labelY);
            }
        }

        private void DrawCamera(Graphics g, CameraData camera, bool isDragging, int index)
        {
            float centerX = camera.X;
            float centerY = camera.Y;
            
            // Use diamond dimensions from camera data (inherited from EnemyData)
            float halfWidth = camera.DiamondWidth / 2.0f;
            float halfHeight = camera.DiamondHeight / 2.0f;
            
            // Define the 4 points of the isometric diamond
            PointF[] diamondPoints = new PointF[]
            {
                new PointF(centerX, centerY - halfHeight),                    // Top
                new PointF(centerX + halfWidth, centerY),                     // Right
                new PointF(centerX, centerY + halfHeight),                    // Bottom
                new PointF(centerX - halfWidth, centerY)                      // Left
            };
            
            // Draw filled isometric diamond (blue for cameras)
            using (SolidBrush brush = new SolidBrush(isDragging ? Color.LightBlue : Color.Blue))
            {
                g.FillPolygon(brush, diamondPoints);
            }
            
            // Draw outline
            using (Pen pen = new Pen(Color.White, 2))
            {
                g.DrawPolygon(pen, diamondPoints);
            }
            
            // Draw bounding box if enabled
            if (_showBoundingBoxes)
            {
                Color boxColor = Color.FromArgb(camera.BoundingBoxColorR, camera.BoundingBoxColorG, camera.BoundingBoxColorB);
                DrawBoundingBox3D(g, centerX, centerY, camera.ZHeight, camera.DiamondWidth, camera.DiamondHeight, 64.0f, boxColor);
            }
            
            // Draw sight cone preview - only if ShowCameraCones is enabled
            if (_showCameraCones)
            {
                // Calculate sight cone length (use CameraSightConeLength if set, otherwise DetectionRange)
                // This matches the game's logic in Camera.cs
                float coneLength = camera.CameraSightConeLength > 0 
                    ? camera.CameraSightConeLength 
                    : camera.DetectionRange;
                
                float coneAngleRad = camera.Rotation;
                // Use SightConeAngle (inherited from Enemy) for the cone angle, not SweepAngle
                // SweepAngle is for rotation behavior, SightConeAngle is for detection cone
                // This matches the game's CreateSightConeTexture which uses SightConeAngle
                float sightConeAngleDeg = camera.SightConeAngle > 0 ? camera.SightConeAngle : 60.0f;
                float halfAngleRad = (sightConeAngleDeg * MathF.PI / 180.0f) / 2.0f;
                
                // Calculate cone edges
                float leftAngle = coneAngleRad - halfAngleRad;
                float rightAngle = coneAngleRad + halfAngleRad;
                
                float leftEndX = centerX + (float)Math.Cos(leftAngle) * coneLength;
                float leftEndY = centerY + (float)Math.Sin(leftAngle) * coneLength;
                float rightEndX = centerX + (float)Math.Cos(rightAngle) * coneLength;
                float rightEndY = centerY + (float)Math.Sin(rightAngle) * coneLength;
                
                // Draw sight cone as a filled triangle (cyan/blue shade for cameras)
                PointF[] conePoints = new PointF[]
                {
                    new PointF(centerX, centerY),
                    new PointF(leftEndX, leftEndY),
                    new PointF(rightEndX, rightEndY)
                };
                
                using (SolidBrush coneBrush = new SolidBrush(Color.FromArgb(80, Color.Cyan)))
                {
                    g.FillPolygon(coneBrush, conePoints);
                }
                
                using (Pen conePen = new Pen(Color.FromArgb(150, Color.Cyan), 2))
                {
                    g.DrawLine(conePen, centerX, centerY, leftEndX, leftEndY);
                    g.DrawLine(conePen, centerX, centerY, rightEndX, rightEndY);
                    g.DrawLine(conePen, leftEndX, leftEndY, rightEndX, rightEndY);
                }
            }
            
            // Draw label above the camera - use name from JSON if available, otherwise use index
            string label;
            if (!string.IsNullOrWhiteSpace(camera.Name))
            {
                label = camera.Name;
            }
            else
            {
                label = $"Camera {index}";
            }
            using (Font font = new Font("Arial", 10, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (SolidBrush backgroundBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
            {
                SizeF textSize = g.MeasureString(label, font);
                
                // Position label above the camera (at the top of the diamond)
                float labelX = centerX - textSize.Width / 2.0f;
                float labelY = centerY - halfHeight - textSize.Height - 4; // 4 pixels above the diamond
                
                // Draw background rectangle for better visibility
                RectangleF backgroundRect = new RectangleF(
                    labelX - 2,
                    labelY - 1,
                    textSize.Width + 4,
                    textSize.Height + 2
                );
                g.FillRectangle(backgroundBrush, backgroundRect);
                
                // Draw text
                g.DrawString(label, font, textBrush, labelX, labelY);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _updateTimer?.Stop();
                _updateTimer?.Dispose();
                _textureLoader?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

