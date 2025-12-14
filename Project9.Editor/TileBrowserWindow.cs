using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Project9.Shared;

namespace Project9.Editor
{
    /// <summary>
    /// Window for browsing and selecting tiles
    /// </summary>
    public class TileBrowserWindow : Form
    {
        private Panel _tilesPanel = null!;
        private Button _dockButton = null!;
        private Label _titleLabel = null!;
        private TileTextureLoader? _textureLoader;
        private MapRenderControl? _mapRenderControl;
        private TerrainType _selectedTerrainType = TerrainType.Grass;
        private PictureBox? _selectedTileBox = null;
        private bool _isDocked = false;
        private Form? _parentForm;
        private bool _isDragging = false;
        private Point _dragStartPosition;
        private Point _dragStartMousePosition;

        /// <summary>
        /// Event raised when docking state changes
        /// </summary>
        public event EventHandler? DockingChanged;

        public bool IsDocked => _isDocked;

        public TileBrowserWindow()
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
            if (_mapRenderControl != null)
            {
                _selectedTerrainType = _mapRenderControl.SelectedTerrainType;
                UpdateSelectedTile();
            }
        }

        public void SetTextureLoader(TileTextureLoader textureLoader)
        {
            _textureLoader = textureLoader;
            LoadTileImages();
        }

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
            this.Text = "Tile Browser";
            this.Size = new Size(300, 500);
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.FromArgb(240, 240, 240);

            // Title panel
            Panel titlePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(45, 45, 48)
            };
            titlePanel.Paint += (s, e) =>
            {
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    titlePanel.ClientRectangle,
                    Color.FromArgb(60, 60, 65),
                    Color.FromArgb(45, 45, 48),
                    System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                {
                    e.Graphics.FillRectangle(brush, titlePanel.ClientRectangle);
                }
                using (var pen = new Pen(Color.FromArgb(30, 30, 30), 1))
                {
                    e.Graphics.DrawLine(pen, 0, titlePanel.Height - 1, titlePanel.Width, titlePanel.Height - 1);
                }
            };

            _titleLabel = new Label
            {
                Text = "Tile Browser",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            _dockButton = new Button
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
            _dockButton.FlatAppearance.BorderSize = 0;
            _dockButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 75);
            _dockButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 50, 55);
            _dockButton.Click += (s, e) =>
            {
                if (_isDocked)
                {
                    Undock();
                }
                else
                {
                    DockToRight();
                }
                _dockButton.Text = _isDocked ? "Undock" : "Dock";
            };

            titlePanel.Controls.Add(_titleLabel);
            titlePanel.Controls.Add(_dockButton);

            // Tiles panel with scrolling
            _tilesPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(10, 10, 10, 10)
            };

            this.Controls.Add(_tilesPanel);
            this.Controls.Add(titlePanel);

            // Enable dragging
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

        private void LoadTileImages()
        {
            _tilesPanel.Controls.Clear();

            if (_textureLoader == null) return;

            int tileSize = 80;
            int padding = 10;
            int x = padding;
            int y = padding;

            foreach (TerrainType terrainType in Enum.GetValues<TerrainType>())
            {
                var texture = _textureLoader.GetTexture(terrainType);
                if (texture == null) continue;

                PictureBox tileBox = new PictureBox
                {
                    Location = new Point(x, y),
                    Size = new Size(tileSize, tileSize),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle,
                    Tag = terrainType,
                    Cursor = Cursors.Hand,
                    BackColor = Color.FromArgb(240, 240, 240)
                };

                // Scale the image to fit
                var scaledImage = ScaleImage(texture, tileSize - 4, tileSize - 4);
                tileBox.Image = scaledImage;

                tileBox.Click += (s, e) =>
                {
                    if (s is PictureBox pb && pb.Tag is TerrainType type)
                    {
                        SelectTile(type, pb);
                    }
                };

                tileBox.MouseEnter += (s, e) =>
                {
                    if (s is PictureBox pb)
                    {
                        pb.BackColor = Color.FromArgb(220, 220, 220);
                    }
                };

                tileBox.MouseLeave += (s, e) =>
                {
                    if (s is PictureBox pb && pb.Tag is TerrainType type)
                    {
                        pb.BackColor = type == _selectedTerrainType 
                            ? Color.FromArgb(173, 216, 230) 
                            : Color.FromArgb(240, 240, 240);
                    }
                };

                _tilesPanel.Controls.Add(tileBox);

                x += tileSize + padding;
                if (x + tileSize + padding > _tilesPanel.Width - SystemInformation.VerticalScrollBarWidth)
                {
                    x = padding;
                    y += tileSize + padding;
                }
            }
        }

        private Bitmap ScaleImage(Bitmap original, int maxWidth, int maxHeight)
        {
            float ratio = Math.Min((float)maxWidth / original.Width, (float)maxHeight / original.Height);
            int newWidth = (int)(original.Width * ratio);
            int newHeight = (int)(original.Height * ratio);

            Bitmap scaled = new Bitmap(newWidth, newHeight);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(original, 0, 0, newWidth, newHeight);
            }
            return scaled;
        }

        private void SelectTile(TerrainType terrainType, PictureBox tileBox)
        {
            // Update previous selection
            if (_selectedTileBox != null)
            {
                _selectedTileBox.BackColor = Color.FromArgb(240, 240, 240);
            }

            // Update new selection
            _selectedTerrainType = terrainType;
            _selectedTileBox = tileBox;
            tileBox.BackColor = Color.FromArgb(173, 216, 230); // Light blue

            // Update map render control
            if (_mapRenderControl != null)
            {
                _mapRenderControl.SelectedTerrainType = terrainType;
            }
        }

        private void UpdateSelectedTile()
        {
            foreach (Control control in _tilesPanel.Controls)
            {
                if (control is PictureBox pb && pb.Tag is TerrainType type)
                {
                    pb.BackColor = type == _selectedTerrainType
                        ? Color.FromArgb(173, 216, 230)
                        : Color.FromArgb(240, 240, 240);
                    
                    if (type == _selectedTerrainType)
                    {
                        _selectedTileBox = pb;
                    }
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing || e.CloseReason == CloseReason.None)
            {
                e.Cancel = true;
                this.Hide();
            }
            base.OnFormClosing(e);
        }
    }
}

