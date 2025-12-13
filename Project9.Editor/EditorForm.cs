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
        private ToolStrip _toolStrip = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _positionLabel = null!;
        private ToolStripStatusLabel _zoomLabel = null!;
        private ToolStripStatusLabel _comfyUIStatusLabel = null!;
        private MenuStrip _menuStrip = null!;
        private EditorMapData _mapData = null!;
        private TileTextureLoader _textureLoader = null!;
        private System.Windows.Forms.Timer _statusUpdateTimer = null!;
        private bool _collisionMode = false;
        private ToolStripButton? _collisionButton = null!;
        private TrackBar? _opacitySlider = null;
        private ComfyUISettings _comfyUISettings = null!;
        private ComfyUIServerManager? _autoStartedServerManager = null;
        private EnemyPropertiesWindow? _enemyPropertiesWindow;
        private PlayerPropertiesWindow? _playerPropertiesWindow;
        private CameraPropertiesWindow? _cameraPropertiesWindow;

        public EditorForm()
        {
            InitializeComponent();
            this.FormClosing += EditorForm_FormClosing;
            InitializeEditor();
        }

        private void EditorForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
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

            _menuStrip.Items.Add(fileMenu);
            
            // Tools Menu
            ToolStripMenuItem toolsMenu = new ToolStripMenuItem("Tools");
            
            ToolStripMenuItem comfyUIMenuItem = new ToolStripMenuItem("Generate Tiles from ComfyUI...");
            comfyUIMenuItem.Click += ComfyUIMenuItem_Click;
            toolsMenu.DropDownItems.Add(comfyUIMenuItem);
            
            toolsMenu.DropDownItems.Add(new ToolStripSeparator());
            
            ToolStripMenuItem comfyUISettingsMenuItem = new ToolStripMenuItem("ComfyUI Settings...");
            comfyUISettingsMenuItem.Click += ComfyUISettingsMenuItem_Click;
            toolsMenu.DropDownItems.Add(comfyUISettingsMenuItem);
            
            _menuStrip.Items.Add(toolsMenu);
            
            // View Menu
            ToolStripMenuItem viewMenu = new ToolStripMenuItem("View");
            
            ToolStripMenuItem propertiesWindowMenuItem = new ToolStripMenuItem("Enemy Properties");
            propertiesWindowMenuItem.Click += PropertiesWindowMenuItem_Click;
            viewMenu.DropDownItems.Add(propertiesWindowMenuItem);
            
            _menuStrip.Items.Add(viewMenu);
            
            // About Menu
            ToolStripMenuItem aboutMenu = new ToolStripMenuItem("About");
            aboutMenu.Click += AboutMenu_Click;
            _menuStrip.Items.Add(aboutMenu);
            
            this.MainMenuStrip = _menuStrip;

            // Tool Strip for tile selection
            _toolStrip = new ToolStrip();
            _toolStrip.Dock = DockStyle.Top;
            
            // Add label
            ToolStripLabel label = new ToolStripLabel("Tile Type:");
            _toolStrip.Items.Add(label);

            // Add buttons for each terrain type (except Test, which gets its own button)
            foreach (TerrainType terrainType in Enum.GetValues<TerrainType>())
            {
                if (terrainType == TerrainType.Test)
                    continue; // Skip Test, add it separately below
                    
                ToolStripButton button = new ToolStripButton(terrainType.ToString());
                button.Tag = terrainType;
                button.Click += TileTypeButton_Click;
                button.DisplayStyle = ToolStripItemDisplayStyle.Text;
                _toolStrip.Items.Add(button);
            }

            // Add separator
            _toolStrip.Items.Add(new ToolStripSeparator());

            // Add dedicated Test Tile button (prominent)
            ToolStripButton testTileButton = new ToolStripButton("Test Tile")
            {
                Tag = TerrainType.Test,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                BackColor = Color.LightGreen // Make it stand out
            };
            testTileButton.Click += TileTypeButton_Click;
            _toolStrip.Items.Add(testTileButton);

            // Add separator
            _toolStrip.Items.Add(new ToolStripSeparator());

            // Add checkbox for 64x32 grid
            ToolStripControlHost gridCheckBoxHost = new ToolStripControlHost(new CheckBox
            {
                Text = "Show 64x32 Grid",
                AutoSize = true,
                Checked = false
            });
            ((CheckBox)gridCheckBoxHost.Control).CheckedChanged += ShowGridCheckBox_CheckedChanged;
            _toolStrip.Items.Add(gridCheckBoxHost);

            // Add separator
            _toolStrip.Items.Add(new ToolStripSeparator());

            // Add opacity slider
            ToolStripLabel opacityLabel = new ToolStripLabel("Tile Opacity:");
            _toolStrip.Items.Add(opacityLabel);

            _opacitySlider = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 70, // Default to 70% (0.7f)
                Width = 150,
                TickFrequency = 10,
                AutoSize = false
            };
            ToolStripControlHost opacitySliderHost = new ToolStripControlHost(_opacitySlider);
            _opacitySlider.ValueChanged += (sender, e) =>
            {
                if (_mapRenderControl != null)
                {
                    _mapRenderControl.TileOpacity = _opacitySlider.Value / 100.0f;
                }
            };
            _toolStrip.Items.Add(opacitySliderHost);

            // Add separator
            _toolStrip.Items.Add(new ToolStripSeparator());

            // Add collision mode button (toggle button)
            _collisionButton = new ToolStripButton("Collision Mode")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                CheckOnClick = true // Make it a toggle button
            };
            _collisionButton.Click += CollisionButton_Click;
            _toolStrip.Items.Add(_collisionButton);

            // Add delete all collision button
            var deleteAllCollisionButton = new ToolStripButton("Delete All Collision")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            deleteAllCollisionButton.Click += DeleteAllCollisionButton_Click;
            _toolStrip.Items.Add(deleteAllCollisionButton);

            // Map Render Control
            _mapRenderControl = new MapRenderControl();
            _mapRenderControl.Dock = DockStyle.Fill;
            _mapRenderControl.SelectedTerrainType = TerrainType.Grass;
            // Set initial opacity to match slider
            if (_opacitySlider != null)
            {
                _mapRenderControl.TileOpacity = _opacitySlider.Value / 100.0f;
            }

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
            this.Controls.Add(_mapRenderControl);
            this.Controls.Add(_toolStrip);
            this.Controls.Add(_statusStrip);
            this.Controls.Add(_menuStrip);
            
            // Properties window will be added as a child form when docked
            
            // Subscribe to form resize to adjust map control when properties window is docked
            this.Resize += EditorForm_Resize;

            // Update selected button
            UpdateSelectedTileButton(TerrainType.Grass);

            this.ResumeLayout(false);
            this.PerformLayout();
        }
        
        private void EditorForm_Resize(object? sender, EventArgs e)
        {
            // Adjust map control if properties window is docked
            if (_enemyPropertiesWindow != null && _enemyPropertiesWindow.IsDocked)
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
            
            // Subscribe to enemy right-click event
            _mapRenderControl.EnemyRightClicked += MapRenderControl_EnemyRightClicked;
            // Subscribe to player right-click event
            _mapRenderControl.PlayerRightClicked += MapRenderControl_PlayerRightClicked;
            // Subscribe to camera right-click event
            _mapRenderControl.CameraRightClicked += MapRenderControl_CameraRightClicked;
            
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

        private void TileTypeButton_Click(object? sender, EventArgs e)
        {
            if (sender is ToolStripButton button && button.Tag is TerrainType terrainType)
            {
                _mapRenderControl.SelectedTerrainType = terrainType;
                UpdateSelectedTileButton(terrainType);
            }
        }

        private void UpdateSelectedTileButton(TerrainType selectedType)
        {
            foreach (ToolStripItem item in _toolStrip.Items)
            {
                if (item is ToolStripButton button && button.Tag is TerrainType terrainType)
                {
                    button.BackColor = terrainType == selectedType ? Color.LightBlue : Color.Transparent;
                }
            }
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

        private void AboutMenu_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("Project 9 V002", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            if (_enemyPropertiesWindow == null || _enemyPropertiesWindow.IsDisposed)
            {
                _enemyPropertiesWindow = new EnemyPropertiesWindow();
                _enemyPropertiesWindow.SetSaveCallback(() => SaveEnemyProperties());
                _enemyPropertiesWindow.Owner = this;
                _enemyPropertiesWindow.SetParentForm(this);
                
                // Subscribe to docking changes
                _enemyPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
            }

            if (_enemyPropertiesWindow.Visible)
            {
                _enemyPropertiesWindow.Hide();
            }
            else
            {
                // Center the window on the editor form
                CenterWindowOnEditor(_enemyPropertiesWindow);
                _enemyPropertiesWindow.Show();
            }
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

        private void AdjustMapControlForDockedWindow()
        {
            // Check if any properties window is docked
            bool enemyWindowDocked = _enemyPropertiesWindow != null && _enemyPropertiesWindow.IsDocked;
            bool playerWindowDocked = _playerPropertiesWindow != null && _playerPropertiesWindow.IsDocked;
            bool cameraWindowDocked = _cameraPropertiesWindow != null && _cameraPropertiesWindow.IsDocked;
            
            if (!enemyWindowDocked && !playerWindowDocked && !cameraWindowDocked)
            {
                // No windows docked - map control fills the form
                _mapRenderControl.Dock = DockStyle.Fill;
                return;
            }
            
            // Window is docked vertically on the right - adjust map control width
            int menuStripHeight = _menuStrip?.Height ?? 0;
            int toolStripHeight = _toolStrip?.Height ?? 0;
            int statusStripHeight = _statusStrip?.Height ?? 0;
            int topPosition = menuStripHeight + toolStripHeight;
            int bottomPosition = this.ClientSize.Height - statusStripHeight;
            
            // Properties window is 300px wide when docked
            int propertiesWindowWidth = 300;
            
            _mapRenderControl.Dock = DockStyle.None;
            _mapRenderControl.Location = new Point(0, topPosition);
            _mapRenderControl.Width = this.ClientSize.Width - propertiesWindowWidth;
            _mapRenderControl.Height = bottomPosition - topPosition;
            _mapRenderControl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
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
                
                // Subscribe to docking changes
                _playerPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
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
                
                // Subscribe to docking changes
                _cameraPropertiesWindow.DockingChanged += (s, e) => AdjustMapControlForDockedWindow();
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

        private void ShowGridCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (_mapRenderControl != null && sender is CheckBox checkBox)
            {
                _mapRenderControl.ShowGrid64x32 = checkBox.Checked;
            }
        }

        private void CollisionButton_Click(object? sender, EventArgs e)
        {
            // The button's Checked state determines collision mode
            _collisionMode = _collisionButton?.Checked ?? false;
            if (_collisionButton != null)
            {
                _collisionButton.BackColor = _collisionMode ? Color.LightBlue : Color.Transparent;
            }
            if (_mapRenderControl != null)
            {
                _mapRenderControl.CollisionMode = _collisionMode;
            }
        }

        private void DeleteAllCollisionButton_Click(object? sender, EventArgs e)
        {
            // Confirm deletion
            DialogResult result = MessageBox.Show(
                "Are you sure you want to delete all collision cells? This action cannot be undone.",
                "Delete All Collision Cells",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                if (_mapRenderControl != null)
                {
                    _mapRenderControl.ClearAllCollisionCells();
                    MessageBox.Show("All collision cells have been deleted.", "Delete All Collision", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
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

