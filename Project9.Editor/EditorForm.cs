using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Project9.Shared;

namespace Project9.Editor
{
    public partial class EditorForm : Form
    {
        private MapRenderControl _mapRenderControl = null!;
        private Panel _mainContentPanel = null!;
        private Panel _dockFrame = null!; // Main docking frame
        private Panel _leftDockPanel = null!;
        private Panel _rightDockPanel = null!;
        private Panel _mapContainer = null!; // Container for the map in the center
        private Splitter _leftSplitter = null!;
        private Splitter _rightSplitter = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _positionLabel = null!;
        private ToolStripStatusLabel _zoomLabel = null!;
        private ToolStripStatusLabel _comfyUIStatusLabel = null!;
        private MenuStrip _menuStrip = null!;
        private EditorMapData _mapData = null!;
        private TileTextureLoader _textureLoader = null!;
        private System.Windows.Forms.Timer _statusUpdateTimer = null!;
        private ComfyUISettings _comfyUISettings = null!;
        private ComfyUIServerManager? _autoStartedServerManager = null;
        private EnemyPropertiesWindow? _enemyPropertiesWindow;
        private PlayerPropertiesWindow? _playerPropertiesWindow;
        private CameraPropertiesWindow? _cameraPropertiesWindow;
        private WeaponPropertiesWindow? _weaponPropertiesWindow;
        private CollisionWindow? _collisionWindow;
        private TileBrowserWindow? _tileBrowserWindow;
        private TilePropertiesWindow? _tilePropertiesWindow;
        private ToolStripMenuItem? _showEnemyConesMenuItem;
        private ToolStripMenuItem? _showCameraConesMenuItem;
        private ToolStripMenuItem? _showGrid32x16MenuItem;
        private ToolStripMenuItem? _showGrid64x32MenuItem;
        private ToolStripMenuItem? _showGrid128x64MenuItem;
        private ToolStripMenuItem? _showGrid512x256MenuItem;
        private ToolStripMenuItem? _showGrid1024x512MenuItem;
        private ToolStripMenuItem? _showBoundingBoxesMenuItem;
        private ToolStripComboBox? _gridSnapComboBox;
        private ToolStripMenuItem? _showEnemyPropertiesMenuItem;
        private ToolStripMenuItem? _showPlayerPropertiesMenuItem;
        private ToolStripMenuItem? _showCameraPropertiesMenuItem;
        private ToolStripMenuItem? _showWeaponPropertiesMenuItem;
        private ToolStripMenuItem? _showCollisionWindowMenuItem;
        private ToolStripMenuItem? _showTileBrowserMenuItem;

        public EditorForm()
        {
            InitializeComponent();
            this.FormClosing += EditorForm_FormClosing;
            InitializeEditor();
        }

        private void EditorForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Save editor layout automatically on close
            try
            {
                EditorLayout layout = EditorLayout.Load();
                
                // Save main window layout
                layout.MainWindow = EditorLayout.WindowLayout.FromForm(this, false);
                
                // Save property windows layouts
                if (_enemyPropertiesWindow != null && !_enemyPropertiesWindow.IsDisposed)
                {
                    layout.EnemyPropertiesWindow = EditorLayout.WindowLayout.FromForm(
                        _enemyPropertiesWindow,
                        _enemyPropertiesWindow.IsDocked
                    );
                }
                
                if (_playerPropertiesWindow != null && !_playerPropertiesWindow.IsDisposed)
                {
                    layout.PlayerPropertiesWindow = EditorLayout.WindowLayout.FromForm(
                        _playerPropertiesWindow,
                        _playerPropertiesWindow.IsDocked
                    );
                }
                
                if (_cameraPropertiesWindow != null && !_cameraPropertiesWindow.IsDisposed)
                {
                    layout.CameraPropertiesWindow = EditorLayout.WindowLayout.FromForm(
                        _cameraPropertiesWindow,
                        _cameraPropertiesWindow.IsDocked
                    );
                }
                
                if (_weaponPropertiesWindow != null && !_weaponPropertiesWindow.IsDisposed)
                {
                    layout.WeaponPropertiesWindow = EditorLayout.WindowLayout.FromForm(
                        _weaponPropertiesWindow,
                        _weaponPropertiesWindow.IsDocked
                    );
                }
                
                if (_collisionWindow != null && !_collisionWindow.IsDisposed)
                {
                    layout.CollisionWindow = EditorLayout.WindowLayout.FromForm(_collisionWindow, false);
                }
                
                if (_tileBrowserWindow != null && !_tileBrowserWindow.IsDisposed)
                {
                    layout.TileBrowserWindow = EditorLayout.WindowLayout.FromForm(
                        _tileBrowserWindow,
                        _tileBrowserWindow.IsDocked
                    );
                }
                
                // Save view settings (including camera position and zoom)
                layout.View = new EditorLayout.ViewSettings
                {
                    ShowGrid32x16 = _mapRenderControl.ShowGrid32x16,
                    ShowGrid64x32 = _mapRenderControl.ShowGrid64x32,
                    ShowGrid128x64 = _mapRenderControl.ShowGrid128x64,
                    ShowGrid512x256 = _mapRenderControl.ShowGrid512x256,
                    ShowGrid1024x512 = _mapRenderControl.ShowGrid1024x512,
                    TileOpacity = _mapRenderControl.TileOpacity,
                    BoundingBoxOpacity = _mapRenderControl.BoundingBoxOpacity,
                    ShowEnemyCones = _mapRenderControl.ShowEnemyCones,
                    ShowCameraCones = _mapRenderControl.ShowCameraCones,
                    CameraPositionX = _mapRenderControl.Camera.Position.X,
                    CameraPositionY = _mapRenderControl.Camera.Position.Y,
                    CameraZoom = _mapRenderControl.Camera.Zoom
                };
                
                layout.Save();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error auto-saving editor layout: {ex.Message}");
            }
            
            // Shut down auto-started ComfyUI if enabled
            if (_comfyUISettings?.AutoStartComfyUI == true && _autoStartedServerManager != null)
            {
                _autoStartedServerManager.StopServer();
                _autoStartedServerManager.Dispose();
                _autoStartedServerManager = null;
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = "Tile Editor";
            this.Size = new Size(1200, 800);
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.FromArgb(240, 240, 240); // Light grey background

            // Menu Strip
            _menuStrip = new MenuStrip();
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            
            ToolStripMenuItem loadMenuItem = new ToolStripMenuItem("Load...");
            loadMenuItem.Click += LoadMenuItem_Click;
            fileMenu.DropDownItems.Add(loadMenuItem);

            ToolStripMenuItem saveMenuItem = new ToolStripMenuItem("Save");
            saveMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            saveMenuItem.Click += SaveMenuItem_Click;
            fileMenu.DropDownItems.Add(saveMenuItem);

            ToolStripMenuItem saveAsMenuItem = new ToolStripMenuItem("Save As...");
            saveAsMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            saveAsMenuItem.Click += SaveAsMenuItem_Click;
            fileMenu.DropDownItems.Add(saveAsMenuItem);

            fileMenu.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem saveLayoutMenuItem = new ToolStripMenuItem("Save Editor Layout");
            saveLayoutMenuItem.Click += SaveLayoutMenuItem_Click;
            fileMenu.DropDownItems.Add(saveLayoutMenuItem);

            ToolStripMenuItem loadLayoutMenuItem = new ToolStripMenuItem("Load Editor Layout");
            loadLayoutMenuItem.Click += LoadLayoutMenuItem_Click;
            fileMenu.DropDownItems.Add(loadLayoutMenuItem);

            _menuStrip.Items.Add(fileMenu);
            
            // Tools Menu
            ToolStripMenuItem toolsMenu = new ToolStripMenuItem("Tools");
            
            ToolStripMenuItem comfyUIMenuItem = new ToolStripMenuItem("Generate Tiles from ComfyUI...");
            comfyUIMenuItem.Click += ComfyUIMenuItem_Click;
            toolsMenu.DropDownItems.Add(comfyUIMenuItem);
            
            ToolStripMenuItem generateImageMenuItem = new ToolStripMenuItem("Generate Image");
            generateImageMenuItem.Click += GenerateImageMenuItem_Click;
            toolsMenu.DropDownItems.Add(generateImageMenuItem);
            
            toolsMenu.DropDownItems.Add(new ToolStripSeparator());
            
            ToolStripMenuItem comfyUISettingsMenuItem = new ToolStripMenuItem("ComfyUI Settings...");
            comfyUISettingsMenuItem.Click += ComfyUISettingsMenuItem_Click;
            toolsMenu.DropDownItems.Add(comfyUISettingsMenuItem);
            
            toolsMenu.DropDownItems.Add(new ToolStripSeparator());
            
            ToolStripMenuItem collisionModeMenuItem = new ToolStripMenuItem("Collision Mode...");
            collisionModeMenuItem.Click += CollisionModeMenuItem_Click;
            toolsMenu.DropDownItems.Add(collisionModeMenuItem);
            
            ToolStripMenuItem tileBrowserMenuItem = new ToolStripMenuItem("Tile Browser");
            tileBrowserMenuItem.Click += TileBrowserMenuItem_Click;
            toolsMenu.DropDownItems.Add(tileBrowserMenuItem);
            
            _menuStrip.Items.Add(toolsMenu);
            
            // View Menu
            ToolStripMenuItem viewMenu = new ToolStripMenuItem("View");
            
            _showEnemyConesMenuItem = new ToolStripMenuItem("Show Enemy Cones");
            _showEnemyConesMenuItem.CheckOnClick = true;
            _showEnemyConesMenuItem.Checked = true; // Default to showing enemy cones
            _showEnemyConesMenuItem.Click += ShowEnemyConesMenuItem_Click;
            viewMenu.DropDownItems.Add(_showEnemyConesMenuItem);
            
            _showCameraConesMenuItem = new ToolStripMenuItem("Show Camera Cones");
            _showCameraConesMenuItem.CheckOnClick = true;
            _showCameraConesMenuItem.Checked = true; // Default to showing camera cones
            _showCameraConesMenuItem.Click += ShowCameraConesMenuItem_Click;
            viewMenu.DropDownItems.Add(_showCameraConesMenuItem);
            
            _showGrid32x16MenuItem = new ToolStripMenuItem("Show 32x16 Grid");
            _showGrid32x16MenuItem.CheckOnClick = true;
            _showGrid32x16MenuItem.Checked = false; // Default to off
            _showGrid32x16MenuItem.Click += ShowGrid32x16MenuItem_Click;
            viewMenu.DropDownItems.Add(_showGrid32x16MenuItem);
            
            _showGrid64x32MenuItem = new ToolStripMenuItem("Show 64x32 Grid");
            _showGrid64x32MenuItem.CheckOnClick = true;
            _showGrid64x32MenuItem.Checked = false; // Default to off
            _showGrid64x32MenuItem.Click += ShowGrid64x32MenuItem_Click;
            viewMenu.DropDownItems.Add(_showGrid64x32MenuItem);
            
            _showGrid128x64MenuItem = new ToolStripMenuItem("Show 128x64 Grid");
            _showGrid128x64MenuItem.CheckOnClick = true;
            _showGrid128x64MenuItem.Checked = false; // Default to off
            _showGrid128x64MenuItem.Click += ShowGrid128x64MenuItem_Click;
            viewMenu.DropDownItems.Add(_showGrid128x64MenuItem);
            
            _showGrid512x256MenuItem = new ToolStripMenuItem("Show 512x256 Grid");
            _showGrid512x256MenuItem.CheckOnClick = true;
            _showGrid512x256MenuItem.Checked = false; // Default to off
            _showGrid512x256MenuItem.Click += ShowGrid512x256MenuItem_Click;
            viewMenu.DropDownItems.Add(_showGrid512x256MenuItem);
            
            _showGrid1024x512MenuItem = new ToolStripMenuItem("Show 1024x512 Grid");
            _showGrid1024x512MenuItem.CheckOnClick = true;
            _showGrid1024x512MenuItem.Checked = false; // Default to off
            _showGrid1024x512MenuItem.Click += ShowGrid1024x512MenuItem_Click;
            viewMenu.DropDownItems.Add(_showGrid1024x512MenuItem);
            
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            
            _showBoundingBoxesMenuItem = new ToolStripMenuItem("Show Bounding Boxes");
            _showBoundingBoxesMenuItem.CheckOnClick = true;
            _showBoundingBoxesMenuItem.Checked = true; // Default to on
            _showBoundingBoxesMenuItem.Click += ShowBoundingBoxesMenuItem_Click;
            viewMenu.DropDownItems.Add(_showBoundingBoxesMenuItem);
            
            ToolStripMenuItem tileOpacityMenuItem = new ToolStripMenuItem("Tile Opacity...");
            tileOpacityMenuItem.Click += TileOpacityMenuItem_Click;
            viewMenu.DropDownItems.Add(tileOpacityMenuItem);
            
            ToolStripMenuItem boundingBoxOpacityMenuItem = new ToolStripMenuItem("Bounding Box Opacity...");
            boundingBoxOpacityMenuItem.Click += BoundingBoxOpacityMenuItem_Click;
            viewMenu.DropDownItems.Add(boundingBoxOpacityMenuItem);
            
            _menuStrip.Items.Add(viewMenu);
            
            // Windows Menu
            ToolStripMenuItem windowsMenu = new ToolStripMenuItem("Windows");
            
            _showEnemyPropertiesMenuItem = new ToolStripMenuItem("Enemy Properties");
            _showEnemyPropertiesMenuItem.CheckOnClick = true;
            _showEnemyPropertiesMenuItem.Checked = false;
            _showEnemyPropertiesMenuItem.Click += ShowEnemyPropertiesMenuItem_Click;
            windowsMenu.DropDownItems.Add(_showEnemyPropertiesMenuItem);
            
            _showPlayerPropertiesMenuItem = new ToolStripMenuItem("Player Properties");
            _showPlayerPropertiesMenuItem.CheckOnClick = true;
            _showPlayerPropertiesMenuItem.Checked = false;
            _showPlayerPropertiesMenuItem.Click += ShowPlayerPropertiesMenuItem_Click;
            windowsMenu.DropDownItems.Add(_showPlayerPropertiesMenuItem);
            
            _showCameraPropertiesMenuItem = new ToolStripMenuItem("Camera Properties");
            _showCameraPropertiesMenuItem.CheckOnClick = true;
            _showCameraPropertiesMenuItem.Checked = false;
            _showCameraPropertiesMenuItem.Click += ShowCameraPropertiesMenuItem_Click;
            windowsMenu.DropDownItems.Add(_showCameraPropertiesMenuItem);
            
            _showWeaponPropertiesMenuItem = new ToolStripMenuItem("Weapon Properties");
            _showWeaponPropertiesMenuItem.CheckOnClick = true;
            _showWeaponPropertiesMenuItem.Checked = false;
            _showWeaponPropertiesMenuItem.Click += ShowWeaponPropertiesMenuItem_Click;
            windowsMenu.DropDownItems.Add(_showWeaponPropertiesMenuItem);
            
            windowsMenu.DropDownItems.Add(new ToolStripSeparator());
            
            _showCollisionWindowMenuItem = new ToolStripMenuItem("Collision Window");
            _showCollisionWindowMenuItem.CheckOnClick = true;
            _showCollisionWindowMenuItem.Checked = false;
            _showCollisionWindowMenuItem.Click += ShowCollisionWindowMenuItem_Click;
            windowsMenu.DropDownItems.Add(_showCollisionWindowMenuItem);
            
            _showTileBrowserMenuItem = new ToolStripMenuItem("Tile Browser");
            _showTileBrowserMenuItem.CheckOnClick = true;
            _showTileBrowserMenuItem.Checked = false;
            _showTileBrowserMenuItem.Click += ShowTileBrowserMenuItem_Click;
            windowsMenu.DropDownItems.Add(_showTileBrowserMenuItem);
            
            _menuStrip.Items.Add(windowsMenu);
            
            // About Menu
            ToolStripMenuItem aboutMenu = new ToolStripMenuItem("About");
            aboutMenu.Click += AboutMenu_Click;
            _menuStrip.Items.Add(aboutMenu);
            
            // Grid Snap Size dropdown
            _menuStrip.Items.Add(new ToolStripSeparator());
            ToolStripLabel gridSnapLabel = new ToolStripLabel("Grid Snap:");
            _menuStrip.Items.Add(gridSnapLabel);
            
            _gridSnapComboBox = new ToolStripComboBox
            {
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _gridSnapComboBox.Items.AddRange(new object[] { "32x16", "64x32", "128x64", "512x256", "1024x512" });
            _gridSnapComboBox.SelectedIndex = 1; // Default to 64x32
            _gridSnapComboBox.SelectedIndexChanged += GridSnapComboBox_SelectedIndexChanged;
            _menuStrip.Items.Add(_gridSnapComboBox);
            
            this.MainMenuStrip = _menuStrip;

            // Main content panel - holds the docking frame
            _mainContentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(3) // Border around the entire docking frame
            };
            _mainContentPanel.Paint += (s, e) =>
            {
                // Draw border around the main content panel
                using (Pen pen = new Pen(Color.FromArgb(160, 160, 160), 3))
                {
                    Rectangle rect = new Rectangle(0, 0, _mainContentPanel.Width - 1, _mainContentPanel.Height - 1);
                    e.Graphics.DrawRectangle(pen, rect);
                }
            };

            // Main docking frame - holds map and dock panels
            _dockFrame = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(250, 250, 250)
            };

            // Map container panel (center area)
            _mapContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            // Left dock panel (initially hidden, width 0)
            _leftDockPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 0,
                BackColor = Color.FromArgb(240, 240, 240),
                Visible = false,
                Padding = new Padding(3)
            };
            _leftDockPanel.Paint += (s, e) =>
            {
                // Draw thick border on right side
                using (Pen pen = new Pen(Color.FromArgb(160, 160, 160), 3))
                {
                    e.Graphics.DrawLine(pen, _leftDockPanel.Width - 1, 0, _leftDockPanel.Width - 1, _leftDockPanel.Height);
                }
            };

            // Left splitter (resizable border)
            _leftSplitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = 3,
                BackColor = Color.FromArgb(160, 160, 160),
                Visible = false,
                MinExtra = 0,
                MinSize = 50
            };
            _leftSplitter.SplitterMoved += (s, e) =>
            {
                // Ensure minimum width after splitter moves
                if (_leftDockPanel.Width < 50)
                {
                    _leftDockPanel.Width = 50;
                }
                _leftDockPanel.Invalidate();
            };

            // Right splitter (resizable border)
            _rightSplitter = new Splitter
            {
                Dock = DockStyle.Right,
                Width = 3,
                BackColor = Color.FromArgb(160, 160, 160),
                Visible = false,
                MinExtra = 0,
                MinSize = 50
            };
            _rightSplitter.SplitterMoved += (s, e) =>
            {
                // Ensure minimum width after splitter moves
                if (_rightDockPanel.Width < 50)
                {
                    _rightDockPanel.Width = 50;
                }
                _rightDockPanel.Invalidate();
            };

            // Right dock panel (initially hidden, width 0)
            _rightDockPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 0,
                BackColor = Color.FromArgb(240, 240, 240),
                Visible = false,
                Padding = new Padding(3)
            };
            _rightDockPanel.Paint += (s, e) =>
            {
                // Draw thick border on left side
                using (Pen pen = new Pen(Color.FromArgb(160, 160, 160), 3))
                {
                    e.Graphics.DrawLine(pen, 0, 0, 0, _rightDockPanel.Height);
                }
            };

            // Map Render Control
            _mapRenderControl = new MapRenderControl();
            _mapRenderControl.Dock = DockStyle.Fill;
            _mapRenderControl.SelectedTerrainType = TerrainType.Grass;
            // Set initial opacity to 70%
            _mapRenderControl.TileOpacity = 0.7f;

            // Add map to map container
            _mapContainer.Controls.Add(_mapRenderControl);

            // Add controls to dock frame in order: right panel, right splitter, map container, left splitter, left panel
            _dockFrame.Controls.Add(_rightDockPanel);
            _dockFrame.Controls.Add(_rightSplitter);
            _dockFrame.Controls.Add(_mapContainer);
            _dockFrame.Controls.Add(_leftSplitter);
            _dockFrame.Controls.Add(_leftDockPanel);

            // Add dock frame to main content panel
            _mainContentPanel.Controls.Add(_dockFrame);

            // Status Strip
            _statusStrip = new StatusStrip();
            _positionLabel = new ToolStripStatusLabel("Position: (0, 0)");
            _zoomLabel = new ToolStripStatusLabel("Zoom: 1.0x");
            _comfyUIStatusLabel = new ToolStripStatusLabel("ComfyUI: Not started");
            _comfyUIStatusLabel.Spring = true; // Take up remaining space
            _comfyUIStatusLabel.TextAlign = ContentAlignment.MiddleRight;
            _statusStrip.Items.Add(_positionLabel);
            _statusStrip.Items.Add(_zoomLabel);
            _statusStrip.Items.Add(_comfyUIStatusLabel);

            // Layout (order matters for z-ordering)
            // Add controls in reverse order of desired z-order (last added is on top)
            this.Controls.Add(_mainContentPanel);
            this.Controls.Add(_statusStrip);
            this.Controls.Add(_menuStrip);
            
            // Properties window will be added as a child form when docked
            
            // Subscribe to form resize to adjust map control when properties window is docked
            this.Resize += EditorForm_Resize;

            this.ResumeLayout(false);
            this.PerformLayout();
        }
        
        private void EditorForm_Resize(object? sender, EventArgs e)
        {
            // Adjust map control if any properties window is docked
            if ((_enemyPropertiesWindow != null && _enemyPropertiesWindow.IsDocked) ||
                (_playerPropertiesWindow != null && _playerPropertiesWindow.IsDocked) ||
                (_cameraPropertiesWindow != null && _cameraPropertiesWindow.IsDocked) ||
                (_weaponPropertiesWindow != null && _weaponPropertiesWindow.IsDocked))
            {
                AdjustMapControlForDockedWindow();
            }
        }

        private async void InitializeEditor()
        {
            // Load ComfyUI settings
            _comfyUISettings = ComfyUISettings.Load();
            
            // Auto-start ComfyUI if enabled
            if (_comfyUISettings.AutoStartComfyUI)
            {
                await AutoStartComfyUIAsync();
            }
            
            // Initialize map data and texture loader
            _mapData = new EditorMapData();
            _textureLoader = new TileTextureLoader();
            
            // Load textures
            _textureLoader.LoadTextures();
            
            // Load map
            await _mapData.LoadAsync();
            
            // Initialize map render control
            _mapRenderControl.Initialize(_mapData, _textureLoader);
            
            // Initialize grid snap size (default to 64x32)
            if (_gridSnapComboBox != null)
            {
                _gridSnapComboBox.SelectedIndex = 1; // 64x32
                _mapRenderControl.GridSnapWidth = 64.0f;
            }
            
            // Load view settings from saved layout
            EditorLayout savedLayout = EditorLayout.Load();
            if (savedLayout.View != null)
            {
                _mapRenderControl.ShowGrid32x16 = savedLayout.View.ShowGrid32x16;
                _mapRenderControl.ShowGrid64x32 = savedLayout.View.ShowGrid64x32;
                _mapRenderControl.ShowGrid128x64 = savedLayout.View.ShowGrid128x64;
                _mapRenderControl.ShowGrid512x256 = savedLayout.View.ShowGrid512x256;
                _mapRenderControl.ShowGrid1024x512 = savedLayout.View.ShowGrid1024x512;
                _mapRenderControl.TileOpacity = savedLayout.View.TileOpacity;
                _mapRenderControl.BoundingBoxOpacity = savedLayout.View.BoundingBoxOpacity;
                _mapRenderControl.ShowEnemyCones = savedLayout.View.ShowEnemyCones;
                _mapRenderControl.ShowCameraCones = savedLayout.View.ShowCameraCones;
                
                // Restore camera position and zoom
                _mapRenderControl.Camera.Position = new System.Drawing.PointF(
                    savedLayout.View.CameraPositionX,
                    savedLayout.View.CameraPositionY
                );
                _mapRenderControl.Camera.Zoom = savedLayout.View.CameraZoom;
                
                // Update menu item checked states
                if (_showGrid32x16MenuItem != null)
                {
                    _showGrid32x16MenuItem.Checked = savedLayout.View.ShowGrid32x16;
                }
                if (_showGrid64x32MenuItem != null)
                {
                    _showGrid64x32MenuItem.Checked = savedLayout.View.ShowGrid64x32;
                }
                if (_showGrid128x64MenuItem != null)
                {
                    _showGrid128x64MenuItem.Checked = savedLayout.View.ShowGrid128x64;
                }
                if (_showGrid512x256MenuItem != null)
                {
                    _showGrid512x256MenuItem.Checked = savedLayout.View.ShowGrid512x256;
                }
                if (_showGrid1024x512MenuItem != null)
                {
                    _showGrid1024x512MenuItem.Checked = savedLayout.View.ShowGrid1024x512;
                }
                if (_showEnemyConesMenuItem != null)
                {
                    _showEnemyConesMenuItem.Checked = savedLayout.View.ShowEnemyCones;
                }
                if (_showCameraConesMenuItem != null)
                {
                    _showCameraConesMenuItem.Checked = savedLayout.View.ShowCameraCones;
                }
            }
            else
            {
                // Set default show cones state
                _mapRenderControl.ShowEnemyCones = true;
                _mapRenderControl.ShowCameraCones = true;
            }
            
            // Open tile browser by default
            OpenTileBrowser();
            
            // Subscribe to enemy right-click event
            _mapRenderControl.EnemyRightClicked += MapRenderControl_EnemyRightClicked;
            // Subscribe to player right-click event
            _mapRenderControl.PlayerRightClicked += MapRenderControl_PlayerRightClicked;
            // Subscribe to camera right-click event
            _mapRenderControl.CameraRightClicked += MapRenderControl_CameraRightClicked;
            // Subscribe to weapon right-click event
            _mapRenderControl.WeaponRightClicked += MapRenderControl_WeaponRightClicked;
            
            // Subscribe to left-click events for populating blank properties windows
            _mapRenderControl.EnemyLeftClicked += MapRenderControl_EnemyLeftClicked;
            _mapRenderControl.PlayerLeftClicked += MapRenderControl_PlayerLeftClicked;
            _mapRenderControl.CameraLeftClicked += MapRenderControl_CameraLeftClicked;
            _mapRenderControl.WeaponLeftClicked += MapRenderControl_WeaponLeftClicked;
            
            // Force a redraw after initialization
            _mapRenderControl.Invalidate();
            
            // Start status update timer
            _statusUpdateTimer = new System.Windows.Forms.Timer();
            _statusUpdateTimer.Interval = 100; // Update every 100ms
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();
        }

        private async Task AutoStartComfyUIAsync()
        {
            try
            {
                // Check if paths are configured
                if (string.IsNullOrEmpty(_comfyUISettings.ComfyUIPythonPath) || 
                    string.IsNullOrEmpty(_comfyUISettings.ComfyUIInstallPath))
                {
                    Console.WriteLine("[Editor] ComfyUI paths not configured - skipping auto-start");
                    UpdateStatusSafe("ComfyUI: Not configured (auto-start skipped)");
                    return;
                }

                UpdateStatusSafe("ComfyUI: Starting...");
                Console.WriteLine("[Editor] Auto-starting ComfyUI server...");

                // Create server manager
                _autoStartedServerManager = new ComfyUIServerManager(
                    _comfyUISettings.ServerUrl,
                    _comfyUISettings.ComfyUIPythonPath,
                    _comfyUISettings.ComfyUIInstallPath
                );

                // Report progress to status label
                var progress = new Progress<string>(status => 
                {
                    Console.WriteLine($"[Editor] {status}");
                    UpdateStatusSafe($"ComfyUI: {status}");
                });
                
                bool success = await _autoStartedServerManager.EnsureServerRunningAsync(progress);
                
                if (success)
                {
                    UpdateStatusSafe("ComfyUI: Running ✓");
                    Console.WriteLine("[Editor] ComfyUI server started successfully");
                }
                else
                {
                    UpdateStatusSafe("ComfyUI: Failed to start");
                    Console.WriteLine("[Editor] ComfyUI server failed to start");
                    
                    // Show error dialog to user
                    MessageBox.Show(
                        "ComfyUI server failed to start automatically. Please check:\n\n" +
                        "1. Python path is correct\n" +
                        "2. ComfyUI installation path is correct\n" +
                        "3. main.py exists in ComfyUI directory\n\n" +
                        "You can configure these in ComfyUI Settings.",
                        "ComfyUI Auto-Start Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                // Report error instead of silently swallowing
                string errorMsg = $"ComfyUI: Error - {ex.Message}";
                UpdateStatusSafe(errorMsg);
                Console.WriteLine($"[Editor] ComfyUI auto-start error: {ex.Message}");
                Console.WriteLine($"[Editor] Stack trace: {ex.StackTrace}");
                
                // Show error dialog
                MessageBox.Show(
                    $"Error auto-starting ComfyUI:\n\n{ex.Message}\n\n" +
                    "You can start ComfyUI manually or disable auto-start in settings.",
                    "ComfyUI Auto-Start Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                
                _autoStartedServerManager?.Dispose();
                _autoStartedServerManager = null;
            }
        }
        
        /// <summary>
        /// Thread-safe status update
        /// </summary>
        private void UpdateStatusSafe(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(message)));
            }
            else
            {
                UpdateStatus(message);
            }
        }
        
        /// <summary>
        /// Update status label
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (_comfyUIStatusLabel != null)
            {
                _comfyUIStatusLabel.Text = message;
            }
        }

        private void StatusUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_mapRenderControl != null)
            {
                PointF pos = _mapRenderControl.Camera.Position;
                float zoom = _mapRenderControl.Camera.Zoom;
                _positionLabel.Text = $"Position: ({pos.X:F1}, {pos.Y:F1})";
                _zoomLabel.Text = $"Zoom: {zoom:F2}x";
            }
        }

        private void TileBrowserMenuItem_Click(object? sender, EventArgs e)
        {
            OpenTileBrowser();
        }

        private void OpenTileBrowser()
        {
            if (_tileBrowserWindow == null || _tileBrowserWindow.IsDisposed)
            {
                _tileBrowserWindow = new TileBrowserWindow();
                _tileBrowserWindow.Owner = this;
                _tileBrowserWindow.SetParentForm(this);
                _tileBrowserWindow.SetMapRenderControl(_mapRenderControl);
                _tileBrowserWindow.SetTextureLoader(_textureLoader);
                _tileBrowserWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                _tileBrowserWindow.VisibleChanged += (s, e) => UpdateTileBrowserMenuItemChecked();
                _tileBrowserWindow.TileRightClicked += TileBrowserWindow_TileRightClicked;
            }

            if (_tileBrowserWindow.Visible)
            {
                _tileBrowserWindow.BringToFront();
                }
                else
                {
                CenterWindowOnEditor(_tileBrowserWindow);
                _tileBrowserWindow.Show();
            }
            UpdateTileBrowserMenuItemChecked();
        }

        private async void LoadMenuItem_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                dialog.Title = "Load Map";
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        await _mapData.LoadAsync(dialog.FileName);
                        _mapRenderControl.Initialize(_mapData, _textureLoader);
                        MessageBox.Show("Map loaded successfully.", "Load Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading map: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async void SaveMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                await _mapData.SaveAsync();
                // Collision cells are saved automatically when placed/removed
                MessageBox.Show("Map saved successfully.", "Save Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving map: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void SaveAsMenuItem_Click(object? sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                dialog.Title = "Save Map As";
                dialog.FileName = "world.json";
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        await _mapData.SaveAsync(dialog.FileName);
                        MessageBox.Show("Map saved successfully.", "Save Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving map: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ShowEnemyPropertiesMenuItem_Click(object? sender, EventArgs e)
        {
            if (_enemyPropertiesWindow == null || _enemyPropertiesWindow.IsDisposed)
            {
                _enemyPropertiesWindow = new EnemyPropertiesWindow();
                _enemyPropertiesWindow.SetSaveCallback(() => SaveEnemyProperties());
                _enemyPropertiesWindow.Owner = this;
                _enemyPropertiesWindow.SetParentForm(this);
                _enemyPropertiesWindow.SetMapRenderControl(_mapRenderControl);
                _enemyPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                _enemyPropertiesWindow.VisibleChanged += (s, e) => UpdateEnemyPropertiesMenuItemChecked();
            }

            if (_enemyPropertiesWindow.Visible)
            {
                _enemyPropertiesWindow.Hide();
            }
            else
            {
                CenterWindowOnEditor(_enemyPropertiesWindow);
                _enemyPropertiesWindow.Show();
            }
            UpdateEnemyPropertiesMenuItemChecked();
        }

        private void ShowPlayerPropertiesMenuItem_Click(object? sender, EventArgs e)
        {
            if (_playerPropertiesWindow == null || _playerPropertiesWindow.IsDisposed)
            {
                _playerPropertiesWindow = new PlayerPropertiesWindow();
                _playerPropertiesWindow.SetSaveCallback(() => SavePlayerProperties());
                _playerPropertiesWindow.Owner = this;
                _playerPropertiesWindow.SetParentForm(this);
                _playerPropertiesWindow.SetMapRenderControl(_mapRenderControl);
                _playerPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                _playerPropertiesWindow.VisibleChanged += (s, e) => UpdatePlayerPropertiesMenuItemChecked();
            }

            if (_playerPropertiesWindow.Visible)
            {
                _playerPropertiesWindow.Hide();
            }
            else
            {
                CenterWindowOnEditor(_playerPropertiesWindow);
                _playerPropertiesWindow.Show();
            }
            UpdatePlayerPropertiesMenuItemChecked();
        }

        private void ShowCameraPropertiesMenuItem_Click(object? sender, EventArgs e)
        {
            if (_cameraPropertiesWindow == null || _cameraPropertiesWindow.IsDisposed)
            {
                _cameraPropertiesWindow = new CameraPropertiesWindow();
                _cameraPropertiesWindow.SetSaveCallback(() => SaveCameraProperties());
                _cameraPropertiesWindow.Owner = this;
                _cameraPropertiesWindow.SetParentForm(this);
                _cameraPropertiesWindow.SetMapRenderControl(_mapRenderControl);
                _cameraPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                _cameraPropertiesWindow.VisibleChanged += (s, e) => UpdateCameraPropertiesMenuItemChecked();
            }

            if (_cameraPropertiesWindow.Visible)
            {
                _cameraPropertiesWindow.Hide();
            }
            else
            {
                CenterWindowOnEditor(_cameraPropertiesWindow);
                _cameraPropertiesWindow.Show();
            }
            UpdateCameraPropertiesMenuItemChecked();
        }

        private void ShowWeaponPropertiesMenuItem_Click(object? sender, EventArgs e)
        {
            if (_weaponPropertiesWindow == null || _weaponPropertiesWindow.IsDisposed)
            {
                _weaponPropertiesWindow = new WeaponPropertiesWindow();
                _weaponPropertiesWindow.SetSaveCallback(() => SaveWeaponProperties());
                _weaponPropertiesWindow.Owner = this;
                _weaponPropertiesWindow.SetParentForm(this);
                _weaponPropertiesWindow.SetMapRenderControl(_mapRenderControl);
                _weaponPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                _weaponPropertiesWindow.VisibleChanged += (s, e) => UpdateWeaponPropertiesMenuItemChecked();
            }

            if (_weaponPropertiesWindow.Visible)
            {
                _weaponPropertiesWindow.Hide();
            }
            else
            {
                CenterWindowOnEditor(_weaponPropertiesWindow);
                _weaponPropertiesWindow.Show();
            }
            UpdateWeaponPropertiesMenuItemChecked();
        }

        private void ShowCollisionWindowMenuItem_Click(object? sender, EventArgs e)
        {
            if (_collisionWindow == null || _collisionWindow.IsDisposed)
            {
                _collisionWindow = new CollisionWindow();
                _collisionWindow.Owner = this;
                _collisionWindow.SetMapRenderControl(_mapRenderControl);
                _collisionWindow.VisibleChanged += (s, e) => UpdateCollisionWindowMenuItemChecked();
            }

            if (_collisionWindow.Visible)
            {
                _collisionWindow.Hide();
            }
            else
            {
                CenterWindowOnEditor(_collisionWindow);
                _collisionWindow.Show();
            }
            UpdateCollisionWindowMenuItemChecked();
        }

        private void ShowTileBrowserMenuItem_Click(object? sender, EventArgs e)
        {
            if (_tileBrowserWindow == null || _tileBrowserWindow.IsDisposed)
            {
                _tileBrowserWindow = new TileBrowserWindow();
                _tileBrowserWindow.Owner = this;
                _tileBrowserWindow.SetParentForm(this);
                _tileBrowserWindow.SetMapRenderControl(_mapRenderControl);
                _tileBrowserWindow.SetTextureLoader(_textureLoader);
                _tileBrowserWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                _tileBrowserWindow.VisibleChanged += (s, e) => UpdateTileBrowserMenuItemChecked();
                _tileBrowserWindow.TileRightClicked += TileBrowserWindow_TileRightClicked;
            }

            if (_tileBrowserWindow.Visible)
            {
                _tileBrowserWindow.Hide();
            }
            else
            {
                if (!_tileBrowserWindow.Visible)
                {
                    CenterWindowOnEditor(_tileBrowserWindow);
                }
                _tileBrowserWindow.Show();
                _tileBrowserWindow.BringToFront();
            }
            UpdateTileBrowserMenuItemChecked();
        }

        private void UpdateEnemyPropertiesMenuItemChecked()
        {
            if (_showEnemyPropertiesMenuItem != null && _enemyPropertiesWindow != null && !_enemyPropertiesWindow.IsDisposed)
            {
                _showEnemyPropertiesMenuItem.Checked = _enemyPropertiesWindow.Visible;
            }
        }

        private void UpdatePlayerPropertiesMenuItemChecked()
        {
            if (_showPlayerPropertiesMenuItem != null && _playerPropertiesWindow != null && !_playerPropertiesWindow.IsDisposed)
            {
                _showPlayerPropertiesMenuItem.Checked = _playerPropertiesWindow.Visible;
            }
        }

        private void UpdateCameraPropertiesMenuItemChecked()
        {
            if (_showCameraPropertiesMenuItem != null && _cameraPropertiesWindow != null && !_cameraPropertiesWindow.IsDisposed)
            {
                _showCameraPropertiesMenuItem.Checked = _cameraPropertiesWindow.Visible;
            }
        }

        private void UpdateWeaponPropertiesMenuItemChecked()
        {
            if (_showWeaponPropertiesMenuItem != null && _weaponPropertiesWindow != null && !_weaponPropertiesWindow.IsDisposed)
            {
                _showWeaponPropertiesMenuItem.Checked = _weaponPropertiesWindow.Visible;
            }
        }

        private void UpdateCollisionWindowMenuItemChecked()
        {
            if (_showCollisionWindowMenuItem != null && _collisionWindow != null && !_collisionWindow.IsDisposed)
            {
                _showCollisionWindowMenuItem.Checked = _collisionWindow.Visible;
            }
        }

        private void UpdateTileBrowserMenuItemChecked()
        {
            if (_showTileBrowserMenuItem != null && _tileBrowserWindow != null && !_tileBrowserWindow.IsDisposed)
            {
                _showTileBrowserMenuItem.Checked = _tileBrowserWindow.Visible;
            }
        }

        private void AboutMenu_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("Project 9 V002", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void ShowEnemyConesMenuItem_Click(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem)
            {
                _mapRenderControl.ShowEnemyCones = menuItem.Checked;
            }
        }
        
        private void ShowCameraConesMenuItem_Click(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem)
            {
                _mapRenderControl.ShowCameraCones = menuItem.Checked;
            }
        }

        private void MapRenderControl_EnemyRightClicked(object? sender, EnemyRightClickedEventArgs e)
        {
            // Create or show properties window
            if (_enemyPropertiesWindow == null || _enemyPropertiesWindow.IsDisposed)
            {
                _enemyPropertiesWindow = new EnemyPropertiesWindow();
                _enemyPropertiesWindow.SetSaveCallback(() => SaveEnemyProperties());
                _enemyPropertiesWindow.Owner = this;
                _enemyPropertiesWindow.SetParentForm(this);
                _enemyPropertiesWindow.SetMapRenderControl(_mapRenderControl);
                
                // Subscribe to docking changes to adjust map control
                _enemyPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
            }

            // Set the selected enemy
            _enemyPropertiesWindow.CurrentEnemy = e.Enemy;
            
            // Show the window (bring to front if already visible)
            if (!_enemyPropertiesWindow.Visible)
            {
                // Center the window on the editor form
                CenterWindowOnEditor(_enemyPropertiesWindow);
                    _enemyPropertiesWindow.Show();
                }
                else
                {
                _enemyPropertiesWindow.BringToFront();
            }
        }

        private void PropertiesWindowMenuItem_Click(object? sender, EventArgs e)
        {
            ShowEnemyPropertiesMenuItem_Click(sender, e);
        }

        /// <summary>
        /// Centers a form window on the editor form
        /// </summary>
        private void CenterWindowOnEditor(Form window)
        {
            if (window == null || this.IsDisposed) return;
            
            // Calculate center position relative to the editor form
            int centerX = this.Left + (this.Width - window.Width) / 2;
            int centerY = this.Top + (this.Height - window.Height) / 2;
            
            // Ensure the window stays on screen
            Screen screen = Screen.FromControl(this);
            centerX = Math.Max(screen.WorkingArea.Left, Math.Min(centerX, screen.WorkingArea.Right - window.Width));
            centerY = Math.Max(screen.WorkingArea.Top, Math.Min(centerY, screen.WorkingArea.Bottom - window.Height));
            
            window.StartPosition = FormStartPosition.Manual;
            window.Location = new Point(centerX, centerY);
        }

        /// <summary>
        /// Gets the right dock panel for docking windows
        /// </summary>
        public Panel GetRightDockPanel()
        {
            return _rightDockPanel;
        }

        /// <summary>
        /// Gets the count of visible docked windows in the right panel
        /// </summary>
        public int GetDockedWindowCount()
        {
            int count = 0;
            foreach (Control control in _rightDockPanel.Controls)
            {
                if (control is Form form && form.Visible)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Gets the left dock panel for docking windows
        /// </summary>
        public Panel GetLeftDockPanel()
        {
            return _leftDockPanel;
        }

        /// <summary>
        /// Shows the right splitter when right panel has content
        /// </summary>
        public void ShowRightSplitter()
        {
            if (_rightDockPanel.Controls.Count > 0 && _rightDockPanel.Visible)
            {
                _rightSplitter.Visible = true;
                _rightSplitter.BringToFront();
            }
        }

        /// <summary>
        /// Shows the left splitter when left panel has content
        /// </summary>
        public void ShowLeftSplitter()
        {
            if (_leftDockPanel.Controls.Count > 0 && _leftDockPanel.Visible)
            {
                _leftSplitter.Visible = true;
                _leftSplitter.BringToFront();
            }
            }

        /// <summary>
        /// Gets the right splitter for external access
        /// </summary>
        public Splitter? GetRightSplitter()
        {
            return _rightSplitter;
        }

        /// <summary>
        /// Gets the left splitter for external access
        /// </summary>
        public Splitter? GetLeftSplitter()
        {
            return _leftSplitter;
        }

        private void AdjustMapControlForDockedWindow()
        {
            // Check if any properties window is docked
            bool enemyWindowDocked = _enemyPropertiesWindow != null && _enemyPropertiesWindow.IsDocked;
            bool playerWindowDocked = _playerPropertiesWindow != null && _playerPropertiesWindow.IsDocked;
            bool cameraWindowDocked = _cameraPropertiesWindow != null && _cameraPropertiesWindow.IsDocked;
            bool weaponWindowDocked = _weaponPropertiesWindow != null && _weaponPropertiesWindow.IsDocked;
            bool tileBrowserWindowDocked = _tileBrowserWindow != null && _tileBrowserWindow.IsDocked;
            
            // Update dock panel visibility and width based on whether any window is docked
            bool anyWindowDocked = enemyWindowDocked || playerWindowDocked || cameraWindowDocked || weaponWindowDocked || tileBrowserWindowDocked;
            if (anyWindowDocked && !_rightDockPanel.Visible)
            {
                _rightDockPanel.Visible = true;
                _rightDockPanel.Width = 300 + 6; // Add 6 pixels for border (3px padding on each side)
                _rightSplitter.Visible = true;
                _rightDockPanel.Invalidate();
            }
            else if (!anyWindowDocked && _rightDockPanel.Visible)
            {
                // Only hide if no windows are actually in the panel
                if (_rightDockPanel.Controls.Count == 0)
                {
                    _rightDockPanel.Visible = false;
                    _rightDockPanel.Width = 0;
                    _rightSplitter.Visible = false;
                }
            }

            // Map control automatically fills remaining space due to Dock.Fill in mainContentPanel
        }

        private void MapRenderControl_PlayerRightClicked(object? sender, PlayerRightClickedEventArgs e)
        {
            // Create or show properties window
            if (_playerPropertiesWindow == null || _playerPropertiesWindow.IsDisposed)
            {
                _playerPropertiesWindow = new PlayerPropertiesWindow();
                _playerPropertiesWindow.SetSaveCallback(() => SavePlayerProperties());
                _playerPropertiesWindow.Owner = this;
                _playerPropertiesWindow.SetParentForm(this);
                _playerPropertiesWindow.SetMapRenderControl(_mapRenderControl);
                
                // Subscribe to docking changes
                _playerPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                _playerPropertiesWindow.VisibleChanged += (s, e) => UpdatePlayerPropertiesMenuItemChecked();
            }

            // Set the selected player
            _playerPropertiesWindow.CurrentPlayer = e.Player;
            
            // Show the window (bring to front if already visible)
            if (!_playerPropertiesWindow.Visible)
            {
                // Center the window on the editor form
                CenterWindowOnEditor(_playerPropertiesWindow);
                    _playerPropertiesWindow.Show();
                }
                else
                {
                _playerPropertiesWindow.BringToFront();
            }
            UpdatePlayerPropertiesMenuItemChecked();
        }

        private async void SaveEnemyProperties()
        {
            try
            {
                await _mapData.SaveAsync();
                _mapRenderControl?.Invalidate(); // Refresh the view to show updated positions/properties
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving enemy properties: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private async void SavePlayerProperties()
        {
            try
            {
                await _mapData.SaveAsync();
                _mapRenderControl?.Invalidate(); // Refresh the view to show updated positions/properties
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving player properties: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void MapRenderControl_CameraRightClicked(object? sender, CameraRightClickedEventArgs e)
        {
            // Create or show properties window
            if (_cameraPropertiesWindow == null || _cameraPropertiesWindow.IsDisposed)
            {
                _cameraPropertiesWindow = new CameraPropertiesWindow();
                _cameraPropertiesWindow.SetSaveCallback(() => SaveCameraProperties());
                _cameraPropertiesWindow.Owner = this;
                _cameraPropertiesWindow.SetParentForm(this);
                _cameraPropertiesWindow.SetMapRenderControl(_mapRenderControl);
                
                // Subscribe to docking changes
                _cameraPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                _cameraPropertiesWindow.VisibleChanged += (s, e) => UpdateCameraPropertiesMenuItemChecked();
            }

            // Set the selected camera
            _cameraPropertiesWindow.CurrentCamera = e.Camera;
            
            // Show the window (bring to front if already visible)
            if (!_cameraPropertiesWindow.Visible)
            {
                // Center the window on the editor form
                CenterWindowOnEditor(_cameraPropertiesWindow);
                    _cameraPropertiesWindow.Show();
                }
                else
                {
                _cameraPropertiesWindow.BringToFront();
            }
            UpdateCameraPropertiesMenuItemChecked();
        }
        
        private async void SaveCameraProperties()
        {
            try
            {
                await _mapData.SaveAsync();
                _mapRenderControl?.Invalidate(); // Refresh the view to show updated positions/properties
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving camera properties: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void MapRenderControl_WeaponRightClicked(object? sender, WeaponRightClickedEventArgs e)
        {
            // Create or show properties window
            if (_weaponPropertiesWindow == null || _weaponPropertiesWindow.IsDisposed)
            {
                _weaponPropertiesWindow = new WeaponPropertiesWindow();
                _weaponPropertiesWindow.SetSaveCallback(() => SaveWeaponProperties());
                _weaponPropertiesWindow.Owner = this;
                _weaponPropertiesWindow.SetParentForm(this);
                _weaponPropertiesWindow.SetMapRenderControl(_mapRenderControl);
                
                // Subscribe to docking changes
                _weaponPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                _weaponPropertiesWindow.VisibleChanged += (s, e) => UpdateWeaponPropertiesMenuItemChecked();
            }

            // Set the selected weapon
            _weaponPropertiesWindow.CurrentWeapon = e.Weapon;
            
            // Show the window (bring to front if already visible)
            if (!_weaponPropertiesWindow.Visible)
            {
                // Center the window on the editor form
                CenterWindowOnEditor(_weaponPropertiesWindow);
                    _weaponPropertiesWindow.Show();
                }
                else
                {
                _weaponPropertiesWindow.BringToFront();
            }
            UpdateWeaponPropertiesMenuItemChecked();
        }
        
        private async void SaveWeaponProperties()
        {
            try
            {
                await _mapData.SaveAsync();
                _mapRenderControl?.Invalidate(); // Refresh the view to show updated positions/properties
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving weapon properties: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void MapRenderControl_EnemyLeftClicked(object? sender, EnemyRightClickedEventArgs e)
        {
            // If enemy properties window is visible and blank, populate it
            if (_enemyPropertiesWindow != null && !_enemyPropertiesWindow.IsDisposed && 
                _enemyPropertiesWindow.Visible && _enemyPropertiesWindow.CurrentEnemy == null)
            {
                _enemyPropertiesWindow.CurrentEnemy = e.Enemy;
            }
        }
        
        private void MapRenderControl_PlayerLeftClicked(object? sender, PlayerRightClickedEventArgs e)
        {
            // If player properties window is visible and blank, populate it
            if (_playerPropertiesWindow != null && !_playerPropertiesWindow.IsDisposed && 
                _playerPropertiesWindow.Visible && _playerPropertiesWindow.CurrentPlayer == null)
            {
                _playerPropertiesWindow.CurrentPlayer = e.Player;
            }
        }
        
        private void MapRenderControl_CameraLeftClicked(object? sender, CameraRightClickedEventArgs e)
        {
            // If camera properties window is visible and blank, populate it
            if (_cameraPropertiesWindow != null && !_cameraPropertiesWindow.IsDisposed && 
                _cameraPropertiesWindow.Visible && _cameraPropertiesWindow.CurrentCamera == null)
            {
                _cameraPropertiesWindow.CurrentCamera = e.Camera;
            }
        }
        
        private void MapRenderControl_WeaponLeftClicked(object? sender, WeaponRightClickedEventArgs e)
        {
            // If weapon properties window is visible and blank, populate it
            if (_weaponPropertiesWindow != null && !_weaponPropertiesWindow.IsDisposed && 
                _weaponPropertiesWindow.Visible && _weaponPropertiesWindow.CurrentWeapon == null)
            {
                _weaponPropertiesWindow.CurrentWeapon = e.Weapon;
            }
        }
        
        private void TileBrowserWindow_TileRightClicked(object? sender, TileRightClickedEventArgs e)
        {
            // Create or show tile properties window
            if (_tilePropertiesWindow == null || _tilePropertiesWindow.IsDisposed)
            {
                _tilePropertiesWindow = new TilePropertiesWindow();
                _tilePropertiesWindow.SetSaveCallback(() => SaveTileProperties());
                _tilePropertiesWindow.Owner = this;
                _tilePropertiesWindow.SetParentForm(this);
                _tilePropertiesWindow.SetMapRenderControl(_mapRenderControl);
                _tilePropertiesWindow.SetTextureLoader(_textureLoader);
                
                // Subscribe to docking changes
                _tilePropertiesWindow.DockingChanged += (s, ev) => AdjustMapControlForDockedWindow();
            }
            
            // Find an existing tile of this type in the map, or create a temporary one for display
            TileData? existingTile = _mapData?.MapData?.Tiles?.FirstOrDefault(t => t.TerrainType == e.TerrainType);
            if (existingTile == null)
            {
                // Create a temporary tile for display purposes
                existingTile = new TileData
                {
                    X = 0,
                    Y = 0,
                    TerrainType = e.TerrainType
                };
            }
            
            // Set the tile (this will show properties for the terrain type)
            _tilePropertiesWindow.CurrentTile = existingTile;
            
            // Show the window (bring to front if already visible)
            if (!_tilePropertiesWindow.Visible)
            {
                _tilePropertiesWindow.Show();
                CenterWindowOnEditor(_tilePropertiesWindow);
            }
            else
            {
                _tilePropertiesWindow.BringToFront();
            }
        }
        
        private void SaveTileProperties()
        {
            // Save the map data
            if (_mapData != null)
            {
                _mapData.SaveAsync().GetAwaiter().GetResult();
            }
        }

        private void ShowGrid32x16MenuItem_Click(object? sender, EventArgs e)
        {
            if (_mapRenderControl != null && sender is ToolStripMenuItem menuItem)
            {
                _mapRenderControl.ShowGrid32x16 = menuItem.Checked;
            }
        }

        private void ShowGrid64x32MenuItem_Click(object? sender, EventArgs e)
        {
            if (_mapRenderControl != null && sender is ToolStripMenuItem menuItem)
            {
                _mapRenderControl.ShowGrid64x32 = menuItem.Checked;
            }
        }

        private void ShowGrid128x64MenuItem_Click(object? sender, EventArgs e)
        {
            if (_mapRenderControl != null && sender is ToolStripMenuItem menuItem)
            {
                _mapRenderControl.ShowGrid128x64 = menuItem.Checked;
            }
        }

        private void ShowGrid512x256MenuItem_Click(object? sender, EventArgs e)
        {
            if (_mapRenderControl != null && sender is ToolStripMenuItem menuItem)
            {
                _mapRenderControl.ShowGrid512x256 = menuItem.Checked;
            }
        }

        private void ShowGrid1024x512MenuItem_Click(object? sender, EventArgs e)
        {
            if (_mapRenderControl != null && sender is ToolStripMenuItem menuItem)
            {
                _mapRenderControl.ShowGrid1024x512 = menuItem.Checked;
            }
        }
        
        private void ShowBoundingBoxesMenuItem_Click(object? sender, EventArgs e)
        {
            if (_mapRenderControl != null && sender is ToolStripMenuItem menuItem)
            {
                _mapRenderControl.ShowBoundingBoxes = menuItem.Checked;
            }
        }

        private void GridSnapComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_gridSnapComboBox != null && _mapRenderControl != null)
            {
                float gridWidth = _gridSnapComboBox.SelectedIndex switch
                {
                    0 => 32.0f,   // 32x16
                    1 => 64.0f,   // 64x32
                    2 => 128.0f,  // 128x64
                    3 => 512.0f,  // 512x256
                    4 => 1024.0f, // 1024x512
                    _ => 64.0f    // Default
                };
                _mapRenderControl.GridSnapWidth = gridWidth;
                _mapRenderControl.Invalidate(); // Refresh to update grid highlighting
            }
        }
        
        private void TileOpacityMenuItem_Click(object? sender, EventArgs e)
        {
            using (TileOpacityDialog dialog = new TileOpacityDialog())
            {
                dialog.Owner = this;
                dialog.SetMapRenderControl(_mapRenderControl);
                dialog.ShowDialog();
            }
        }
        
        private void BoundingBoxOpacityMenuItem_Click(object? sender, EventArgs e)
        {
            using (BoundingBoxOpacityDialog dialog = new BoundingBoxOpacityDialog())
            {
                dialog.Owner = this;
                dialog.SetMapRenderControl(_mapRenderControl);
                dialog.ShowDialog();
            }
        }

        private void SaveLayoutMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                EditorLayout layout = new EditorLayout();

                // Save main window layout
                layout.MainWindow = EditorLayout.WindowLayout.FromForm(this);

                // Save property windows layouts
                if (_enemyPropertiesWindow != null && !_enemyPropertiesWindow.IsDisposed)
                {
                    layout.EnemyPropertiesWindow = EditorLayout.WindowLayout.FromForm(
                        _enemyPropertiesWindow, 
                        _enemyPropertiesWindow.IsDocked
                    );
                }

                if (_playerPropertiesWindow != null && !_playerPropertiesWindow.IsDisposed)
                {
                    layout.PlayerPropertiesWindow = EditorLayout.WindowLayout.FromForm(
                        _playerPropertiesWindow,
                        _playerPropertiesWindow.IsDocked
                    );
                }

                if (_cameraPropertiesWindow != null && !_cameraPropertiesWindow.IsDisposed)
                {
                    layout.CameraPropertiesWindow = EditorLayout.WindowLayout.FromForm(
                        _cameraPropertiesWindow,
                        _cameraPropertiesWindow.IsDocked
                    );
                }

                if (_weaponPropertiesWindow != null && !_weaponPropertiesWindow.IsDisposed)
                {
                    layout.WeaponPropertiesWindow = EditorLayout.WindowLayout.FromForm(
                        _weaponPropertiesWindow,
                        _weaponPropertiesWindow.IsDocked
                    );
                }

                if (_collisionWindow != null && !_collisionWindow.IsDisposed)
                {
                    layout.CollisionWindow = EditorLayout.WindowLayout.FromForm(_collisionWindow, false);
                }

                if (_tileBrowserWindow != null && !_tileBrowserWindow.IsDisposed)
                {
                    layout.TileBrowserWindow = EditorLayout.WindowLayout.FromForm(
                        _tileBrowserWindow,
                        _tileBrowserWindow.IsDocked
                    );
                }

                // Save view settings
                layout.View = new EditorLayout.ViewSettings
                {
                    ShowGrid32x16 = _mapRenderControl.ShowGrid32x16,
                    ShowGrid64x32 = _mapRenderControl.ShowGrid64x32,
                    ShowGrid128x64 = _mapRenderControl.ShowGrid128x64,
                    ShowGrid512x256 = _mapRenderControl.ShowGrid512x256,
                    ShowGrid1024x512 = _mapRenderControl.ShowGrid1024x512,
                    TileOpacity = _mapRenderControl.TileOpacity,
                    ShowEnemyCones = _mapRenderControl.ShowEnemyCones,
                    ShowCameraCones = _mapRenderControl.ShowCameraCones,
                    CameraPositionX = _mapRenderControl.Camera.Position.X,
                    CameraPositionY = _mapRenderControl.Camera.Position.Y,
                    CameraZoom = _mapRenderControl.Camera.Zoom
                };

                layout.Save();
                MessageBox.Show("Editor layout saved successfully.", "Save Layout", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving editor layout: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadLayoutMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                EditorLayout layout = EditorLayout.Load();

                // Load main window layout
                if (layout.MainWindow != null)
                {
                    layout.MainWindow.ApplyToForm(this, false);
                }

                // Load property windows layouts
                if (layout.EnemyPropertiesWindow != null)
                {
                    if (_enemyPropertiesWindow == null || _enemyPropertiesWindow.IsDisposed)
                    {
                        _enemyPropertiesWindow = new EnemyPropertiesWindow();
                        _enemyPropertiesWindow.SetSaveCallback(() => SaveEnemyProperties());
                        _enemyPropertiesWindow.Owner = this;
                        _enemyPropertiesWindow.SetParentForm(this);
                        _enemyPropertiesWindow.SetMapRenderControl(_mapRenderControl);
                        _enemyPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                        _enemyPropertiesWindow.VisibleChanged += (s, e) => UpdateEnemyPropertiesMenuItemChecked();
                    }
                    layout.EnemyPropertiesWindow.ApplyToForm(_enemyPropertiesWindow, layout.EnemyPropertiesWindow.IsDocked);
                    if (layout.EnemyPropertiesWindow.IsDocked && !_enemyPropertiesWindow.IsDocked)
                    {
                        _enemyPropertiesWindow.DockToRight();
                    }
                    else if (!layout.EnemyPropertiesWindow.IsDocked && _enemyPropertiesWindow.IsDocked)
                    {
                        _enemyPropertiesWindow.Undock();
                    }
                    UpdateEnemyPropertiesMenuItemChecked();
                }

                if (layout.PlayerPropertiesWindow != null)
                {
                    if (_playerPropertiesWindow == null || _playerPropertiesWindow.IsDisposed)
                    {
                        _playerPropertiesWindow = new PlayerPropertiesWindow();
                        _playerPropertiesWindow.SetSaveCallback(() => SavePlayerProperties());
                        _playerPropertiesWindow.Owner = this;
                        _playerPropertiesWindow.SetParentForm(this);
                        _playerPropertiesWindow.SetMapRenderControl(_mapRenderControl);
                        _playerPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                        _playerPropertiesWindow.VisibleChanged += (s, e) => UpdatePlayerPropertiesMenuItemChecked();
                    }
                    layout.PlayerPropertiesWindow.ApplyToForm(_playerPropertiesWindow, layout.PlayerPropertiesWindow.IsDocked);
                    if (layout.PlayerPropertiesWindow.IsDocked && !_playerPropertiesWindow.IsDocked)
                    {
                        _playerPropertiesWindow.DockToRight();
                    }
                    else if (!layout.PlayerPropertiesWindow.IsDocked && _playerPropertiesWindow.IsDocked)
                    {
                        _playerPropertiesWindow.Undock();
                    }
                    UpdatePlayerPropertiesMenuItemChecked();
                }

                if (layout.CameraPropertiesWindow != null)
                {
                    if (_cameraPropertiesWindow == null || _cameraPropertiesWindow.IsDisposed)
                    {
                        _cameraPropertiesWindow = new CameraPropertiesWindow();
                        _cameraPropertiesWindow.SetSaveCallback(() => SaveCameraProperties());
                        _cameraPropertiesWindow.Owner = this;
                        _cameraPropertiesWindow.SetParentForm(this);
                        _cameraPropertiesWindow.SetMapRenderControl(_mapRenderControl);
                        _cameraPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                        _cameraPropertiesWindow.VisibleChanged += (s, e) => UpdateCameraPropertiesMenuItemChecked();
                    }
                    layout.CameraPropertiesWindow.ApplyToForm(_cameraPropertiesWindow, layout.CameraPropertiesWindow.IsDocked);
                    if (layout.CameraPropertiesWindow.IsDocked && !_cameraPropertiesWindow.IsDocked)
                    {
                        _cameraPropertiesWindow.DockToRight();
                    }
                    else if (!layout.CameraPropertiesWindow.IsDocked && _cameraPropertiesWindow.IsDocked)
                    {
                        _cameraPropertiesWindow.Undock();
                    }
                    UpdateCameraPropertiesMenuItemChecked();
                }
                
                if (layout.WeaponPropertiesWindow != null)
                {
                    if (_weaponPropertiesWindow == null || _weaponPropertiesWindow.IsDisposed)
                {
                        _weaponPropertiesWindow = new WeaponPropertiesWindow();
                        _weaponPropertiesWindow.SetSaveCallback(() => SaveWeaponProperties());
                        _weaponPropertiesWindow.Owner = this;
                        _weaponPropertiesWindow.SetParentForm(this);
                        _weaponPropertiesWindow.SetMapRenderControl(_mapRenderControl);
                        _weaponPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                        _weaponPropertiesWindow.VisibleChanged += (s, e) => UpdateWeaponPropertiesMenuItemChecked();
                    }
                    layout.WeaponPropertiesWindow.ApplyToForm(_weaponPropertiesWindow, layout.WeaponPropertiesWindow.IsDocked);
                    if (layout.WeaponPropertiesWindow.IsDocked && !_weaponPropertiesWindow.IsDocked)
                    {
                        _weaponPropertiesWindow.DockToRight();
                    }
                    else if (!layout.WeaponPropertiesWindow.IsDocked && _weaponPropertiesWindow.IsDocked)
                    {
                        _weaponPropertiesWindow.Undock();
                    }
                    UpdateWeaponPropertiesMenuItemChecked();
                }
                
                if (layout.CollisionWindow != null)
                {
                    if (_collisionWindow == null || _collisionWindow.IsDisposed)
                    {
                        _collisionWindow = new CollisionWindow();
                        _collisionWindow.Owner = this;
                        _collisionWindow.SetMapRenderControl(_mapRenderControl);
                        _collisionWindow.VisibleChanged += (s, e) => UpdateCollisionWindowMenuItemChecked();
                    }
                    layout.CollisionWindow.ApplyToForm(_collisionWindow, false);
                    UpdateCollisionWindowMenuItemChecked();
                }

                if (layout.TileBrowserWindow != null)
                {
                    if (_tileBrowserWindow == null || _tileBrowserWindow.IsDisposed)
                    {
                        _tileBrowserWindow = new TileBrowserWindow();
                        _tileBrowserWindow.Owner = this;
                        _tileBrowserWindow.SetParentForm(this);
                        _tileBrowserWindow.SetMapRenderControl(_mapRenderControl);
                        _tileBrowserWindow.SetTextureLoader(_textureLoader);
                        _tileBrowserWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
                        _tileBrowserWindow.VisibleChanged += (s, e) => UpdateTileBrowserMenuItemChecked();
                    }
                    layout.TileBrowserWindow.ApplyToForm(_tileBrowserWindow, layout.TileBrowserWindow.IsDocked);
                    if (layout.TileBrowserWindow.IsDocked && !_tileBrowserWindow.IsDocked)
                    {
                        _tileBrowserWindow.DockToRight();
                    }
                    else if (!layout.TileBrowserWindow.IsDocked && _tileBrowserWindow.IsDocked)
                    {
                        _tileBrowserWindow.Undock();
                    }
                    UpdateTileBrowserMenuItemChecked();
                }

                // Load view settings
                if (layout.View != null)
                {
                    _mapRenderControl.ShowGrid32x16 = layout.View.ShowGrid32x16;
                    _mapRenderControl.ShowGrid64x32 = layout.View.ShowGrid64x32;
                    _mapRenderControl.ShowGrid128x64 = layout.View.ShowGrid128x64;
                    _mapRenderControl.ShowGrid512x256 = layout.View.ShowGrid512x256;
                    _mapRenderControl.ShowGrid1024x512 = layout.View.ShowGrid1024x512;
                    _mapRenderControl.TileOpacity = layout.View.TileOpacity;
                    _mapRenderControl.BoundingBoxOpacity = layout.View.BoundingBoxOpacity;
                    _mapRenderControl.ShowEnemyCones = layout.View.ShowEnemyCones;
                    _mapRenderControl.ShowCameraCones = layout.View.ShowCameraCones;
                    
                    // Restore camera position and zoom
                    _mapRenderControl.Camera.Position = new System.Drawing.PointF(
                        layout.View.CameraPositionX,
                        layout.View.CameraPositionY
                    );
                    _mapRenderControl.Camera.Zoom = layout.View.CameraZoom;
                    _mapRenderControl.Invalidate(); // Refresh the view
                    
                    // Update menu item checked states
                    if (_showGrid32x16MenuItem != null)
                    {
                        _showGrid32x16MenuItem.Checked = layout.View.ShowGrid32x16;
                    }
                    if (_showGrid64x32MenuItem != null)
                    {
                        _showGrid64x32MenuItem.Checked = layout.View.ShowGrid64x32;
                    }
                    if (_showGrid128x64MenuItem != null)
                    {
                        _showGrid128x64MenuItem.Checked = layout.View.ShowGrid128x64;
                    }
                    if (_showGrid512x256MenuItem != null)
                    {
                        _showGrid512x256MenuItem.Checked = layout.View.ShowGrid512x256;
                    }
                    if (_showGrid1024x512MenuItem != null)
                    {
                        _showGrid1024x512MenuItem.Checked = layout.View.ShowGrid1024x512;
                    }
                    if (_showEnemyConesMenuItem != null)
                    {
                        _showEnemyConesMenuItem.Checked = layout.View.ShowEnemyCones;
                    }
                    if (_showCameraConesMenuItem != null)
                    {
                        _showCameraConesMenuItem.Checked = layout.View.ShowCameraCones;
                    }
                }

                AdjustMapControlForDockedWindow();
                MessageBox.Show("Editor layout loaded successfully.", "Load Layout", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading editor layout: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CollisionModeMenuItem_Click(object? sender, EventArgs e)
            {
            if (_collisionWindow == null || _collisionWindow.IsDisposed)
            {
                _collisionWindow = new CollisionWindow();
                _collisionWindow.Owner = this;
                _collisionWindow.SetMapRenderControl(_mapRenderControl);
                _collisionWindow.VisibleChanged += (s, e) => UpdateCollisionWindowMenuItemChecked();
            }
            
            if (_collisionWindow.Visible)
            {
                _collisionWindow.BringToFront();
                }
                else
                {
            // Automatically enable collision mode when showing the window
            if (_mapRenderControl != null)
            {
                _mapRenderControl.CollisionMode = true;
            }
                CenterWindowOnEditor(_collisionWindow);
                _collisionWindow.Show();
            }
            UpdateCollisionWindowMenuItemChecked();
        }

        private void ComfyUISettingsMenuItem_Click(object? sender, EventArgs e)
        {
            using (ComfyUISettingsDialog settingsDialog = new ComfyUISettingsDialog(_comfyUISettings))
            {
                if (settingsDialog.ShowDialog() == DialogResult.OK)
                {
                    _comfyUISettings = settingsDialog.Settings;
                    _comfyUISettings.Save();
                }
            }
        }

        private void GenerateImageMenuItem_Click(object? sender, EventArgs e)
        {
            using (GenerateImageDialog dialog = new GenerateImageDialog())
            {
                dialog.Owner = this;
                dialog.ShowDialog();
            }
        }

        private async void ComfyUIMenuItem_Click(object? sender, EventArgs e)
        {
            // Validate saved settings
            string? workflowPath = null;
            string? outputDirectory = null;

            // Check workflow path
            if (string.IsNullOrEmpty(_comfyUISettings.LastWorkflowPath) || !File.Exists(_comfyUISettings.LastWorkflowPath))
            {
                MessageBox.Show(
                    "No workflow file is configured. Please configure it in ComfyUI Settings.",
                    "ComfyUI Workflow",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                
                // Open settings dialog
                using (ComfyUISettingsDialog settingsDialog = new ComfyUISettingsDialog(_comfyUISettings))
                {
                    if (settingsDialog.ShowDialog() == DialogResult.OK)
                    {
                        _comfyUISettings = settingsDialog.Settings;
                        _comfyUISettings.Save();
                    }
                    else
                    {
                        return; // User cancelled settings
                    }
                }
                
                // Re-check after settings dialog
                if (string.IsNullOrEmpty(_comfyUISettings.LastWorkflowPath) || !File.Exists(_comfyUISettings.LastWorkflowPath))
                {
                    MessageBox.Show(
                        "Workflow file is still not configured. Please configure it in ComfyUI Settings.",
                        "ComfyUI Workflow",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    return;
                }
            }
            
            workflowPath = _comfyUISettings.LastWorkflowPath;

            // Check output directory - default to content\sprites\tiles\comfy
            if (string.IsNullOrEmpty(_comfyUISettings.LastOutputDirectory) || !Directory.Exists(_comfyUISettings.LastOutputDirectory))
            {
                // Use default: content\sprites\tiles\comfy
                outputDirectory = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Content",
                    "sprites",
                    "tiles",
                    "comfy"
                );
                
                // Create default directory if it doesn't exist
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }
                
                // Save default if remember paths is enabled
                if (_comfyUISettings.RememberPaths)
                {
                    _comfyUISettings.LastOutputDirectory = outputDirectory;
                    _comfyUISettings.Save();
                }
            }
            else
            {
                outputDirectory = _comfyUISettings.LastOutputDirectory;
            }

            // Show progress dialog and execute workflow
            ComfyUIProgressDialog progressDialog = new ComfyUIProgressDialog();
            ComfyUIServerManager? serverManager = null;
            
            try
            {
                // Show dialog first so user can see it
                progressDialog.Show();
                progressDialog.UpdateStatus("Initializing workflow execution...");
                
                // Check/start ComfyUI server if auto-start is enabled
                if (_comfyUISettings.AutoStartComfyUI)
                {
                    serverManager = new ComfyUIServerManager(
                        _comfyUISettings.ServerUrl,
                        _comfyUISettings.ComfyUIPythonPath,
                        _comfyUISettings.ComfyUIInstallPath
                    );
                    
                    var progress = new Progress<string>(status => progressDialog.UpdateStatus(status));
                    bool serverReady = await serverManager.EnsureServerRunningAsync(progress, progressDialog.CancellationToken);
                    
                    if (!serverReady)
                    {
                        progressDialog.SetError("Failed to start ComfyUI server. Please check your settings and ensure ComfyUI is installed correctly.");
                        progressDialog.Hide();
                        progressDialog.ShowDialog();
                        progressDialog.Dispose();
                        serverManager?.Dispose();
                        return;
                    }
                }
                else
                {
                    // Just check if server is running
                    using (ComfyUIServerManager checkManager = new ComfyUIServerManager(
                        _comfyUISettings.ServerUrl,
                        null,
                        null))
                    {
                        progressDialog.UpdateStatus("Checking if ComfyUI server is running...");
                        bool isRunning = await checkManager.IsServerRunningAsync(progressDialog.CancellationToken);
                        
                        if (!isRunning)
                        {
                            DialogResult result = MessageBox.Show(
                                "ComfyUI server does not appear to be running.\n\n" +
                                "Would you like to enable auto-start in ComfyUI Settings?\n\n" +
                                "Click 'Yes' to open settings, or 'No' to try anyway.",
                                "ComfyUI Server Not Running",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning
                            );
                            
                            if (result == DialogResult.Yes)
                            {
                                progressDialog.Hide();
                                using (ComfyUISettingsDialog settingsDialog = new ComfyUISettingsDialog(_comfyUISettings))
                                {
                                    if (settingsDialog.ShowDialog() == DialogResult.OK)
                                    {
                                        _comfyUISettings = settingsDialog.Settings;
                                        _comfyUISettings.Save();
                                    }
                                }
                                progressDialog.Dispose();
                                return;
                            }
                        }
                    }
                }
                
                // Open ComfyUI in browser so user can see the workflow running
                try
                {
                    string serverUrl = _comfyUISettings.ServerUrl;
                    if (string.IsNullOrEmpty(serverUrl))
                    {
                        serverUrl = "http://localhost:8188";
                    }
                    
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = serverUrl,
                        UseShellExecute = true
                    });
                    
                    progressDialog.UpdateStatus("Opened ComfyUI in browser. Workflow will be queued...");
                }
                catch
                {
                    // If opening browser fails, continue anyway
                }
                
                // Start async execution
                Task<ExecutionResult> executionTask;
                
                using (ComfyUIWorkflowExecutor executor = new ComfyUIWorkflowExecutor(_comfyUISettings.ServerUrl))
                {
                    var progress = new Progress<string>(status => progressDialog.UpdateStatus(status));
                    
                    executionTask = executor.ExecuteWorkflowAsync(
                        workflowPath,
                        outputDirectory,
                        progress,
                        progressDialog.CancellationToken
                    );
                    
                    // Wait for execution to complete
                    ExecutionResult result = await executionTask;

                    if (result.Success)
                    {
                        progressDialog.SetCompleted();
                        
                        // Wait a moment so user can see completion
                        await Task.Delay(500);
                        
                        // Close the progress dialog
                        progressDialog.Hide();
                        progressDialog.Dispose();
                        
                        MessageBox.Show(
                            $"Workflow completed successfully!\n\nTiles saved to:\n{outputDirectory}",
                            "ComfyUI Workflow",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                        
                        // Optionally refresh texture loader
                        _textureLoader?.LoadTextures();
                        _mapRenderControl?.Invalidate();
                    }
                    else
                    {
                        progressDialog.SetError(result.ErrorMessage ?? "Unknown error occurred");
                        // Hide first, then show modally so user can see the error and close it
                        progressDialog.Hide();
                        progressDialog.ShowDialog();
                        progressDialog.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                progressDialog.SetError($"Error: {ex.Message}\n\nStack trace: {ex.StackTrace}");
                // Hide first, then show modally so user can see the error and close it
                progressDialog.Hide();
                progressDialog.ShowDialog();
                progressDialog.Dispose();
            }
            finally
            {
                serverManager?.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Shut down auto-started ComfyUI if enabled
                if (_comfyUISettings?.AutoStartComfyUI == true && _autoStartedServerManager != null)
                {
                    _autoStartedServerManager.StopServer();
                    _autoStartedServerManager.Dispose();
                    _autoStartedServerManager = null;
                }

                _statusUpdateTimer?.Stop();
                _statusUpdateTimer?.Dispose();
                _textureLoader?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

