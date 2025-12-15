using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Project9.Shared;

namespace Project9.Editor
{
    /// <summary>
    /// Dockable window for editing tile properties
    /// </summary>
    public class TilePropertiesWindow : Form
    {
        private PropertyGrid _propertyGrid = null!;
        private TileData? _currentTile;
        private Button _dockButton = null!;
        private Label _titleLabel = null!;
        private Action? _onSaveCallback;
        private bool _isDocked = false;
        private Form? _parentForm;
        private MapRenderControl? _mapRenderControl;
        private TileTextureLoader? _textureLoader;
        private PictureBox? _previewPictureBox;
        private Button? _loadGraphicButton;
        private Panel? _previewPanel;
        private Panel? _tileInfoPanel;
        private Label? _tileDimensionsLabel;
        private Label? _tileTerrainLabel;
        private bool _isDragging = false;
        private Point _dragStartPosition;
        private Point _dragStartMousePosition;
        
        /// <summary>
        /// Event raised when docking state changes
        /// </summary>
        public event EventHandler? DockingChanged;

        public TileData? CurrentTile
        {
            get => _currentTile;
            set
            {
                _currentTile = value;
                UpdatePropertyGrid();
            }
        }

        public TilePropertiesWindow()
        {
            InitializeComponent();
        }

        public void SetParentForm(Form parentForm)
        {
            _parentForm = parentForm;
            if (_parentForm != null)
            {
                _parentForm.Resize += ParentForm_Resize;
            }
        }
        
        public void SetMapRenderControl(MapRenderControl mapRenderControl)
        {
            _mapRenderControl = mapRenderControl;
        }
        
        public void SetTextureLoader(TileTextureLoader textureLoader)
        {
            _textureLoader = textureLoader;
        }
        
        public bool IsDocked => _isDocked;
        
        public int DockedHeight => _isDocked ? this.Height : 0;
        
        private void ParentForm_Resize(object? sender, EventArgs e)
        {
            // When docked in a panel, the panel handles resizing automatically via Dock.Fill
            // No manual position calculation needed
        }

        public void DockToRight()
        {
            if (_parentForm == null) return;

            _isDocked = true;

            // Get the dock panel from EditorForm
            Panel? dockPanel = null;
            if (_parentForm is EditorForm editorForm)
            {
                dockPanel = editorForm.GetRightDockPanel();
            }

            if (dockPanel != null)
            {
                // Suspend layout on both form and panel
                this.SuspendLayout();
                dockPanel.SuspendLayout();
                
                // Hide the form first before changing parent
                this.Hide();
                
                // Clear Owner - this is critical for embedding
                this.Owner = null;
                
                // Remove from any previous parent
                if (this.Parent != null)
                {
                    this.Parent.Controls.Remove(this);
                }
                
                // Set properties before setting parent
                this.FormBorderStyle = FormBorderStyle.None;
                this.ShowInTaskbar = false;
                this.TopLevel = false; // Must be false BEFORE setting Parent
                
                // Count visible docked windows (excluding splitters)
                int visibleWindowCount = 0;
                foreach (Control control in dockPanel.Controls)
                {
                    if (control is Form otherForm && otherForm.Visible && otherForm != this)
                    {
                        visibleWindowCount++;
                    }
                }
                
                // Calculate height for each window (split space equally)
                int totalWindows = visibleWindowCount + 1; // +1 for this window
                int panelHeight = dockPanel.Height > 0 ? dockPanel.Height : 600; // Default if not yet sized
                int windowHeight = Math.Max(200, panelHeight / totalWindows); // Minimum 200px per window
                
                // If this is the first window, use Fill; otherwise stack with Top
                if (visibleWindowCount == 0)
                {
                    // First window - fills the panel
                    this.Parent = dockPanel;
                    this.Dock = DockStyle.Fill;
                    this.Margin = new Padding(3);
                }
                else
                {
                    // Add a splitter before this window
                    Splitter splitter = new Splitter
                    {
                        Dock = DockStyle.Top,
                        Height = 3,
                        BackColor = Color.FromArgb(160, 160, 160),
                        MinExtra = 100,
                        MinSize = 100
                    };
                    dockPanel.Controls.Add(splitter);
                    
                    // Now set the parent - this embeds it into the panel
                    this.Parent = dockPanel;
                    this.Dock = DockStyle.Top;
                    this.Height = windowHeight;
                    this.Margin = new Padding(3, 3, 3, 0); // Top margin only
                    
                    // Adjust existing windows to share space
                    int newWindowHeight = Math.Max(200, panelHeight / totalWindows);
                    foreach (Control control in dockPanel.Controls)
                    {
                        if (control is Form otherForm && otherForm.Visible && otherForm != this)
                        {
                            // Convert existing Fill windows to Top with fixed height
                            if (otherForm.Dock == DockStyle.Fill)
                            {
                                otherForm.Dock = DockStyle.Top;
                                otherForm.Height = newWindowHeight;
                            }
                            else
                            {
                                otherForm.Height = newWindowHeight;
                            }
                        }
                    }
                }
                
                dockPanel.Width = 300 + 6; // Add 6 pixels for border (3px padding on each side)
                
                // Resume layout
                this.ResumeLayout(false);
                dockPanel.ResumeLayout(false);
                
                // Show the splitter when panel is visible
                if (_parentForm is EditorForm editorForm2)
                {
                    editorForm2.ShowRightSplitter();
                }
                dockPanel.Visible = true;
                dockPanel.Invalidate(); // Refresh border drawing
                
                // Show the form after it's embedded
                this.Show();
                this.BringToFront();
            }
            else
            {
                // Fallback to old behavior if not EditorForm
                this.Hide();
                this.FormBorderStyle = FormBorderStyle.None;
                this.TopLevel = false;
                this.Parent = _parentForm;
                this.Dock = DockStyle.Right;
                this.Width = 300;
                this.Show();
            }

            if (_dockButton != null)
            {
                _dockButton.Text = "Undock";
            }

            DockingChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Undock()
        {
            if (!_isDocked) return;

            _isDocked = false;
            
            // Suspend layout during undocking
            this.SuspendLayout();
            
            // Remove from dock panel
            Panel? dockPanel = null;
            if (_parentForm is EditorForm editorForm)
            {
                dockPanel = editorForm.GetRightDockPanel();
            }

            if (dockPanel != null && dockPanel == this.Parent)
            {
                dockPanel.SuspendLayout();
                this.Margin = Padding.Empty;
                
                // Remove any splitter associated with this window
                List<Control> controlsToRemove = new List<Control>();
                bool foundThisWindow = false;
                for (int i = dockPanel.Controls.Count - 1; i >= 0; i--)
                {
                    Control control = dockPanel.Controls[i];
                    if (control == this)
                    {
                        foundThisWindow = true;
                    }
                    else if (foundThisWindow && control is Splitter)
                    {
                        controlsToRemove.Add(control);
                        break; // Only remove the splitter right before this window
                    }
                }
                foreach (var control in controlsToRemove)
                {
                    dockPanel.Controls.Remove(control);
                }
                
                // Clear parent first, then remove from panel
                this.Parent = null;
                this.TopLevel = true; // Must be set BEFORE removing from controls
                dockPanel.Controls.Remove(this);
                
                // Adjust remaining windows to fill space
                int remainingWindows = 0;
                foreach (Control control in dockPanel.Controls)
                {
                    if (control is Form form && form.Visible)
                    {
                        remainingWindows++;
                    }
                }
                
                if (remainingWindows > 0)
                {
                    int panelHeight = dockPanel.Height > 0 ? dockPanel.Height : 600;
                    if (remainingWindows == 1)
                    {
                        // Only one window left - make it Fill
                        foreach (Control control in dockPanel.Controls)
                        {
                            if (control is Form form && form.Visible)
                            {
                                form.Dock = DockStyle.Fill;
                                form.Margin = new Padding(3);
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Multiple windows - share space equally
                        int newWindowHeight = Math.Max(200, panelHeight / remainingWindows);
                        foreach (Control control in dockPanel.Controls)
                        {
                            if (control is Form form && form.Visible)
                            {
                                form.Dock = DockStyle.Top;
                                form.Height = newWindowHeight;
                            }
                        }
                    }
                }
                
                // Resume dock panel layout
                dockPanel.ResumeLayout(false);
                
                if (remainingWindows == 0)
                {
                    dockPanel.Visible = false;
                    dockPanel.Width = 0;
                    // Hide the splitter when panel is empty
                    if (_parentForm is EditorForm editorForm2)
                    {
                        var splitter = editorForm2.GetRightSplitter();
                        if (splitter != null)
                        {
                            splitter.Visible = false;
                        }
                    }
                }
                else
                {
                    dockPanel.Invalidate(); // Refresh border drawing
                }
            }

            // Set form properties
            this.Dock = DockStyle.None;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.ShowInTaskbar = false;
            
            // Restore Owner AFTER setting TopLevel = true
            if (_parentForm != null)
            {
                this.Owner = _parentForm;
            }
            
            // Position near parent form (only if not being positioned by drag)
            if (_parentForm != null && this.Location.X == 0 && this.Location.Y == 0)
            {
                this.Location = new Point(
                    _parentForm.Right + 10,
                    _parentForm.Top + 50
                );
            }
            
            // Resume layout
            this.ResumeLayout(false);

            if (_dockButton != null)
            {
                _dockButton.Text = "Dock";
            }

            DockingChanged?.Invoke(this, EventArgs.Empty);
        }

        private void InitializeComponent()
        {
            this.Text = "Tile Properties";
            this.Size = new Size(350, 650);
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.FromArgb(240, 240, 240);
            
            // Title panel with gradient background
            Panel titlePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(45, 45, 48)
            };
            titlePanel.Paint += (s, e) =>
            {
                // Draw gradient background
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    titlePanel.ClientRectangle,
                    Color.FromArgb(60, 60, 65),
                    Color.FromArgb(45, 45, 48),
                    System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                {
                    e.Graphics.FillRectangle(brush, titlePanel.ClientRectangle);
                }
                // Draw bottom border
                using (var pen = new Pen(Color.FromArgb(30, 30, 30), 1))
                {
                    e.Graphics.DrawLine(pen, 0, titlePanel.Height - 1, titlePanel.Width, titlePanel.Height - 1);
                }
            };
            
            _titleLabel = new Label
            {
                Text = "Tile Properties",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            
            // Modern styled dock button
            Button dockButton = new Button
            {
                Text = "Dock",
                Dock = DockStyle.Right,
                Width = 70,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Margin = new Padding(5, 5, 5, 5),
                Cursor = Cursors.Hand
            };
            dockButton.FlatAppearance.BorderSize = 0;
            dockButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 75);
            dockButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 50, 55);
            dockButton.Click += (s, e) =>
            {
                if (_isDocked)
                {
                    Undock();
                }
                else
                {
                    DockToRight();
                }
                dockButton.Text = _isDocked ? "Undock" : "Dock";
            };
            
            _dockButton = dockButton;
            
            titlePanel.Controls.Add(_titleLabel);
            titlePanel.Controls.Add(dockButton);

            // Tile Information panel (similar to name panel in other property windows)
            _tileInfoPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = new Padding(10, 8, 10, 8)
            };
            _tileInfoPanel.Paint += (s, e) =>
            {
                // Draw bottom border
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                {
                    e.Graphics.DrawLine(pen, 0, _tileInfoPanel.Height - 1, _tileInfoPanel.Width, _tileInfoPanel.Height - 1);
                }
            };
            
            _tileTerrainLabel = new Label
            {
                Text = "Terrain: None",
                Location = new Point(10, 12),
                Size = new Size(150, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(30, 30, 30),
                Font = new Font("Segoe UI", 9),
                BackColor = Color.Transparent
            };
            
            _tileDimensionsLabel = new Label
            {
                Text = "Size: -",
                Location = new Point(170, 12),
                Size = new Size(150, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(30, 30, 30),
                Font = new Font("Segoe UI", 9),
                BackColor = Color.Transparent
            };
            
            _tileInfoPanel.Controls.Add(_tileTerrainLabel);
            _tileInfoPanel.Controls.Add(_tileDimensionsLabel);

            // Tile preview panel
            _previewPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 150,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(5)
            };
            
            Label previewLabel = new Label
            {
                Text = "Tile Preview",
                Dock = DockStyle.Top,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            
            _previewPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            _previewPanel.Controls.Add(_previewPictureBox);
            _previewPanel.Controls.Add(previewLabel);
            
            // Load new graphic button panel (similar to color picker panel in other property windows)
            Panel loadGraphicPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(245, 245, 245),
                Padding = new Padding(10, 8, 10, 8)
            };
            loadGraphicPanel.Paint += (s, e) =>
            {
                // Draw bottom border
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                {
                    e.Graphics.DrawLine(pen, 0, loadGraphicPanel.Height - 1, loadGraphicPanel.Width, loadGraphicPanel.Height - 1);
                }
            };
            
            Label loadLabel = new Label
            {
                Text = "Load Graphic:",
                Location = new Point(10, 15),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(30, 30, 30)
            };
            
            _loadGraphicButton = new Button
            {
                Text = "Load",
                Location = new Point(115, 10),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.Black,
                BackColor = Color.FromArgb(230, 230, 230),
                Cursor = Cursors.Hand
            };
            _loadGraphicButton.FlatAppearance.BorderSize = 1;
            _loadGraphicButton.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            _loadGraphicButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(210, 210, 210);
            _loadGraphicButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(190, 190, 190);
            _loadGraphicButton.Click += LoadGraphicButton_Click;
            
            loadGraphicPanel.Controls.Add(loadLabel);
            loadGraphicPanel.Controls.Add(_loadGraphicButton);
            
            // Property grid with better styling
            _propertyGrid = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                ToolbarVisible = true,
                HelpVisible = true,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 30, 30),
                Font = new Font("Segoe UI", 9),
                LineColor = Color.FromArgb(230, 230, 230)
            };
            _propertyGrid.PropertyValueChanged += (s, e) =>
            {
                PropertyGrid_PropertyValueChanged(s, e);
                UpdatePreview();
            };
            
            // Bottom panel with save button
            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = new Padding(10, 10, 10, 10)
            };
            bottomPanel.Paint += (s, e) =>
            {
                // Draw top border
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 0, bottomPanel.Width, 0);
                }
            };
            
            // Large save button (blue)
            Button saveButton = new Button
            {
                Text = "Save",
                Dock = DockStyle.Fill,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 120, 215),
                Cursor = Cursors.Hand
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 150, 255);
            saveButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 100, 180);
            saveButton.Click += (s, e) =>
            {
                _onSaveCallback?.Invoke();
            };
            
            bottomPanel.Controls.Add(saveButton);
            
            // Add controls in correct docking order:
            // Bottom-docked first (bottom to top), then Fill, then Top-docked (top to bottom)
            // For top-docked: first added = topmost position
            // For bottom-docked: first added = bottommost position
            this.Controls.Add(bottomPanel);            // Bottom-docked: bottommost
            this.Controls.Add(_previewPanel);          // Bottom-docked: above bottom
            this.Controls.Add(_propertyGrid);          // Fill: takes remaining space
            this.Controls.Add(titlePanel);             // Top-docked: topmost (first top-docked = at top)
            this.Controls.Add(_tileInfoPanel);        // Top-docked: below title (second top-docked)
            this.Controls.Add(loadGraphicPanel);       // Top-docked: below tile info (third top-docked)
            
            // Enable dragging for title panel to undock
            titlePanel.MouseDown += TitlePanel_MouseDown;
            titlePanel.MouseMove += TitlePanel_MouseMove;
            titlePanel.MouseUp += TitlePanel_MouseUp;
            _titleLabel.MouseDown += TitlePanel_MouseDown;
            _titleLabel.MouseMove += TitlePanel_MouseMove;
            _titleLabel.MouseUp += TitlePanel_MouseUp;
        }
        
        private void TitlePanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _isDocked)
            {
                _isDragging = true;
                _dragStartPosition = this.Location;
                Control? control = sender as Control;
                if (control != null)
                {
                    _dragStartMousePosition = control.PointToScreen(e.Location);
                }
                else
                {
                    _dragStartMousePosition = Control.MousePosition;
                }
            }
        }
        
        private void TitlePanel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_isDragging && _isDocked)
            {
                Point currentMousePos = Control.MousePosition;
                int deltaX = Math.Abs(currentMousePos.X - _dragStartMousePosition.X);
                int deltaY = Math.Abs(currentMousePos.Y - _dragStartMousePosition.Y);
                
                if (deltaX > 5 || deltaY > 5)
                {
                    _isDragging = false;
                    Undock();
                    
                    if (_parentForm != null)
                    {
                        Point screenPos = Control.MousePosition;
                        Point clientPos = _parentForm.PointToClient(screenPos);
                        this.Location = new Point(
                            clientPos.X - this.Width / 2,
                            clientPos.Y - 10
                        );
                    }
                }
            }
        }
        
        private void TitlePanel_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = false;
            }
        }

        public void SetSaveCallback(Action? callback)
        {
            _onSaveCallback = callback;
        }

        private void UpdatePropertyGrid()
        {
            if (_currentTile != null)
            {
                // Create a wrapper object for property grid editing
                var wrapper = new TilePropertiesWrapper(_currentTile);
                _propertyGrid.SelectedObject = wrapper;
                UpdateTitle();
                UpdatePreview();
                UpdateTileInfo();
            }
            else
            {
                _propertyGrid.SelectedObject = null;
                _titleLabel.Text = "  Tile Properties  •  No Selection";
                if (_previewPictureBox != null)
                {
                    _previewPictureBox.Image = null;
                }
                ClearTileInfo();
            }
        }
        
        private void UpdateTitle()
        {
            if (_currentTile != null)
            {
                _titleLabel.Text = $"  Tile Properties  •  {_currentTile.TerrainType}  •  X: {_currentTile.X}, Y: {_currentTile.Y}";
            }
        }
        
        private void UpdatePreview()
        {
            if (_currentTile != null && _textureLoader != null && _previewPictureBox != null)
            {
                var texture = _textureLoader.GetTexture(_currentTile.TerrainType);
                if (texture != null)
                {
                    _previewPictureBox.Image = texture;
                }
                else
                {
                    _previewPictureBox.Image = null;
                }
            }
            else if (_previewPictureBox != null)
            {
                _previewPictureBox.Image = null;
            }
        }
        
        private void UpdateTileInfo()
        {
            if (_currentTile == null)
            {
                ClearTileInfo();
                return;
            }
            
            if (_tileTerrainLabel != null)
            {
                _tileTerrainLabel.Text = $"Terrain: {_currentTile.TerrainType}";
            }
            
            // Get texture dimensions if available
            if (_textureLoader != null)
            {
                var texture = _textureLoader.GetTexture(_currentTile.TerrainType);
                if (texture != null)
                {
                    if (_tileDimensionsLabel != null)
                    {
                        _tileDimensionsLabel.Text = $"Size: {texture.Width}×{texture.Height}";
                    }
                }
                else
                {
                    if (_tileDimensionsLabel != null)
                    {
                        _tileDimensionsLabel.Text = "Size: -";
                    }
                }
            }
            else
            {
                if (_tileDimensionsLabel != null)
                {
                    _tileDimensionsLabel.Text = "Size: -";
                }
            }
        }
        
        private void ClearTileInfo()
        {
            if (_tileTerrainLabel != null)
            {
                _tileTerrainLabel.Text = "Terrain: None";
            }
            if (_tileDimensionsLabel != null)
            {
                _tileDimensionsLabel.Text = "Size: -";
            }
        }

        private void PropertyGrid_PropertyValueChanged(object? s, PropertyValueChangedEventArgs e)
        {
            // Auto-save on property change
            _onSaveCallback?.Invoke();
            
            // Update title if X, Y, or TerrainType changed
            if (_currentTile != null && (e.ChangedItem?.Label == "X" || e.ChangedItem?.Label == "Y" || e.ChangedItem?.Label == "TerrainType"))
            {
                UpdateTitle();
                UpdatePreview();
                _mapRenderControl?.Invalidate();
            }
        }
        
        private void LoadGraphicButton_Click(object? sender, EventArgs e)
        {
            if (_currentTile == null || _textureLoader == null) return;
            
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*";
                openFileDialog.Title = "Load New Tile Graphic";
                
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Load the new image
                        Bitmap newImage = new Bitmap(openFileDialog.FileName);
                        
                        // Determine the target path based on terrain type
                        string texturePath;
                        if (_currentTile.TerrainType == TerrainType.Test || _currentTile.TerrainType == TerrainType.Test2)
                        {
                            texturePath = $"Content/sprites/tiles/test/{_currentTile.TerrainType}.png";
                        }
                        else
                        {
                            texturePath = $"Content/sprites/tiles/template/{_currentTile.TerrainType}.png";
                        }
                        
                        // Resolve the actual path
                        string? resolvedPath = ResolveTexturePath(texturePath);
                        
                        if (resolvedPath != null)
                        {
                            // Ensure directory exists
                            string? directory = Path.GetDirectoryName(resolvedPath);
                            if (directory != null && !Directory.Exists(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }
                            
                            // Save the new image
                            newImage.Save(resolvedPath, ImageFormat.Png);
                            
                            // Reload the texture for this terrain type
                            // Dispose old texture if it exists
                            var oldTexture = _textureLoader.GetTexture(_currentTile.TerrainType);
                            if (oldTexture != null)
                            {
                                oldTexture.Dispose();
                            }
                            
                            // Load the new texture
                            Bitmap newTexture = new Bitmap(resolvedPath);
                            
                            // Update the texture in the loader's dictionary (using reflection)
                            // Since TileTextureLoader doesn't expose a SetTexture method, we'll update it directly
                            FieldInfo? texturesField = typeof(TileTextureLoader).GetField("_textures", 
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            if (texturesField != null)
                            {
                                var textures = texturesField.GetValue(_textureLoader) as System.Collections.Generic.Dictionary<TerrainType, Bitmap>;
                                if (textures != null)
                                {
                                    // Dispose old texture if it exists
                                    if (textures.TryGetValue(_currentTile.TerrainType, out var oldTex))
                                    {
                                        oldTex?.Dispose();
                                    }
                                    textures[_currentTile.TerrainType] = newTexture;
                                }
                            }
                            
                            // Update preview and tile info
                            UpdatePreview();
                            UpdateTileInfo();
                            
                            // Refresh the map render and tile browser
                            _mapRenderControl?.Invalidate();
                            
                            MessageBox.Show($"Graphic loaded successfully!\n\nSaved to: {resolvedPath}\n\nAll {_currentTile.TerrainType} tiles will use this new graphic.", 
                                "Graphic Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Could not resolve texture path. Please ensure the Content folder structure exists.", 
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading graphic: {ex.Message}", 
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        
        private static string? ResolveTexturePath(string relativePath)
        {
            // Try current directory first
            string currentDir = Directory.GetCurrentDirectory();
            string fullPath = Path.GetFullPath(Path.Combine(currentDir, relativePath));
            if (File.Exists(fullPath))
                return fullPath;

            // Try executable directory
            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            fullPath = Path.GetFullPath(Path.Combine(exeDir, relativePath));
            if (File.Exists(fullPath))
                return fullPath;

            // Try going up to project root (for development)
            var dir = new DirectoryInfo(exeDir);
            while (dir != null && dir.Parent != null)
            {
                string testPath = Path.GetFullPath(Path.Combine(dir.FullName, relativePath));
                if (File.Exists(testPath))
                    return testPath;
                dir = dir.Parent;
            }
            
            // If file doesn't exist, return where it should be (in project root)
            dir = new DirectoryInfo(exeDir);
            while (dir != null && dir.Parent != null)
            {
                string testPath = Path.GetFullPath(Path.Combine(dir.FullName, relativePath));
                string? testDir = Path.GetDirectoryName(testPath);
                if (testDir != null && Directory.Exists(Path.GetDirectoryName(testDir)))
                {
                    return testPath;
                }
                dir = dir.Parent;
            }

            return null;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Hide instead of close to preserve the window
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Wrapper class to expose TileData properties for PropertyGrid editing
        /// </summary>
        private class TilePropertiesWrapper
        {
            private TileData _tile;

            public TilePropertiesWrapper(TileData tile)
            {
                _tile = tile;
            }

            [System.ComponentModel.Category("Position")]
            [System.ComponentModel.Description("X coordinate in tile space")]
            public int X
            {
                get => _tile.X;
                set => _tile.X = value;
            }

            [System.ComponentModel.Category("Position")]
            [System.ComponentModel.Description("Y coordinate in tile space")]
            public int Y
            {
                get => _tile.Y;
                set => _tile.Y = value;
            }

            [System.ComponentModel.Category("Terrain")]
            [System.ComponentModel.Description("Type of terrain for this tile")]
            public TerrainType TerrainType
            {
                get => _tile.TerrainType;
                set => _tile.TerrainType = value;
            }
        }
    }
}

