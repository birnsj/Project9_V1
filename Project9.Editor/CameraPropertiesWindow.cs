using System;
using System.Drawing;
using System.Windows.Forms;
using Project9.Shared;

namespace Project9.Editor
{
    /// <summary>
    /// Dockable window for editing camera properties
    /// </summary>
    public class CameraPropertiesWindow : Form
    {
        private PropertyGrid _propertyGrid = null!;
        private CameraData? _currentCamera;
        private Button _saveButton = null!;
        private Button _dockButton = null!;
        private Label _titleLabel = null!;
        private TextBox _nameTextBox = null!;
        private Label _nameLabel = null!;
        private Action? _onSaveCallback;
        private Button? _colorPickerButton;
        private bool _isDocked = false;
        private Form? _parentForm;
        private MapRenderControl? _mapRenderControl;
        private bool _isDragging = false;
        private Point _dragStartPosition;
        private Point _dragStartMousePosition;
        
        /// <summary>
        /// Event raised when docking state changes
        /// </summary>
        public event EventHandler? DockingChanged;

        public CameraData? CurrentCamera
        {
            get => _currentCamera;
            set
            {
                _currentCamera = value;
                UpdatePropertyGrid();
            }
        }

        public CameraPropertiesWindow()
        {
            InitializeComponent();
        }

        public void SetParentForm(Form parentForm)
        {
            _parentForm = parentForm;
            // Subscribe to parent form resize to update docked position
            if (_parentForm != null)
            {
                _parentForm.Resize += ParentForm_Resize;
            }
        }
        
        public void SetMapRenderControl(MapRenderControl mapRenderControl)
        {
            _mapRenderControl = mapRenderControl;
        }
        
        public bool IsDocked => _isDocked;
        
        public int DockedWidth => _isDocked ? this.Width : 0;
        
        private void ParentForm_Resize(object? sender, EventArgs e)
        {
            if (_isDocked && _parentForm != null)
            {
                // Recalculate position when parent form resizes
                int menuStripHeight = 0;
                int toolStripHeight = 0;
                
                foreach (Control control in _parentForm.Controls)
                {
                    if (control is MenuStrip menuStrip)
                    {
                        menuStripHeight = menuStrip.Height;
                    }
                    else if (control is ToolStrip toolStrip && control != _parentForm.MainMenuStrip)
                    {
                        toolStripHeight = toolStrip.Height;
                    }
                }
                
                int topPosition = menuStripHeight + toolStripHeight;
                int bottomPosition = _parentForm.ClientSize.Height - (_parentForm.Controls.OfType<StatusStrip>().FirstOrDefault()?.Height ?? 0);
                
                // When docked vertically, maintain fixed width and position on right side
                this.Location = new Point(_parentForm.ClientSize.Width - this.Width, topPosition);
                this.Height = bottomPosition - topPosition;
                // Width stays at 300 (fixed)
            }
        }

        public void DockToRight()
        {
            if (_parentForm == null) return;

            _isDocked = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopLevel = false; // Must be set before Parent
            this.Parent = _parentForm; // This makes it a child window
            
            // Calculate the top position based on menu strip and tool strip heights
            int menuStripHeight = 0;
            int toolStripHeight = 0;
            
            foreach (Control control in _parentForm.Controls)
            {
                if (control is MenuStrip menuStrip)
                {
                    menuStripHeight = menuStrip.Height;
                }
                else if (control is ToolStrip toolStrip && control != _parentForm.MainMenuStrip)
                {
                    toolStripHeight = toolStrip.Height;
                }
            }
            
            int topPosition = menuStripHeight + toolStripHeight;
            int bottomPosition = _parentForm.ClientSize.Height - (_parentForm.Controls.OfType<StatusStrip>().FirstOrDefault()?.Height ?? 0);
            
            // Dock vertically on the right side
            this.Dock = DockStyle.None;
            this.Width = 300; // Fixed width for properties window
            this.Location = new Point(_parentForm.ClientSize.Width - this.Width, topPosition);
            this.Height = bottomPosition - topPosition;
            this.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            
            // Ensure the window is visible and on top
            this.Show();
            this.BringToFront();
            
            // Ensure the properties window is on top of other controls (but below menu/tool/status strips)
            if (_parentForm.Controls.Contains(this))
            {
                // Move to top of z-order, but keep menu/tool/status strips on top
                int maxIndex = _parentForm.Controls.Count - 1;
                int targetIndex = maxIndex;
                
                // Find the index of the last menu/tool/status strip
                for (int i = _parentForm.Controls.Count - 1; i >= 0; i--)
                {
                    var control = _parentForm.Controls[i];
                    if (control is MenuStrip || control is ToolStrip || control is StatusStrip)
                    {
                        targetIndex = i;
                        break;
                    }
                }
                
                // Place properties window just below the strips
                _parentForm.Controls.SetChildIndex(this, targetIndex);
            }
            
            // Update button text
            if (_dockButton != null)
            {
                _dockButton.Text = "Undock";
            }
            
            // Notify that docking changed
            DockingChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Undock()
        {
            if (!_isDocked) return;

            _isDocked = false;
            this.Dock = DockStyle.None;
            this.Parent = null;
            this.TopLevel = true;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            
            // Position near parent form
            if (_parentForm != null)
            {
                this.Location = new Point(
                    _parentForm.Right + 10,
                    _parentForm.Top + 50
                );
            }
            
            // Update button text
            if (_dockButton != null)
            {
                _dockButton.Text = "Dock";
            }
            
            // Notify that docking changed
            DockingChanged?.Invoke(this, EventArgs.Empty);
        }

        private void InitializeComponent()
        {
            this.Text = "Camera Properties";
            this.Size = new Size(350, 650); // 30% taller (500 * 1.3 = 650)
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
                Text = "Camera Properties",
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
                // Update button text after state change
                dockButton.Text = _isDocked ? "Undock" : "Dock";
            };
            
            // Store reference to update button text when docking changes
            _dockButton = dockButton;
            
            titlePanel.Controls.Add(_titleLabel);
            titlePanel.Controls.Add(dockButton);
            
            // Name field panel at the top
            Panel namePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = new Padding(10, 8, 10, 8)
            };
            namePanel.Paint += (s, e) =>
            {
                // Draw bottom border
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                {
                    e.Graphics.DrawLine(pen, 0, namePanel.Height - 1, namePanel.Width, namePanel.Height - 1);
                }
            };
            
            _nameLabel = new Label
            {
                Text = "Name:",
                Location = new Point(10, 12),
                Size = new Size(50, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(30, 30, 30)
            };
            
            _nameTextBox = new TextBox
            {
                Location = new Point(65, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = namePanel.Width - 75,
                Font = new Font("Segoe UI", 9),
                BorderStyle = BorderStyle.FixedSingle
            };
            _nameTextBox.TextChanged += _nameTextBox_TextChanged;
            
            namePanel.Controls.Add(_nameLabel);
            namePanel.Controls.Add(_nameTextBox);
            this.Controls.Add(namePanel);
            
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
                _saveButton.Enabled = true;
                // Update title if Name, X, or Y changed
                if (_currentCamera != null && (e.ChangedItem?.Label == "Name" || e.ChangedItem?.Label == "X" || e.ChangedItem?.Label == "Y"))
                {
                    UpdateTitle();
                }
                
                // If diamond dimensions changed, refresh the view immediately
                if (e.ChangedItem?.Label == "DiamondWidth" || e.ChangedItem?.Label == "DiamondHeight")
                {
                    _mapRenderControl?.Invalidate();
                }
                // Refresh preview when properties change
                foreach (Control control in this.Controls)
                {
                    if (control is Panel panel && panel.BackColor == Color.FromArgb(30, 30, 30))
                    {
                        panel.Invalidate();
                        break;
                    }
                }
            };
            
            // Handle Enter key to commit changes immediately
            _propertyGrid.PreviewKeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    var selectedItem = _propertyGrid.SelectedGridItem;
                    if (selectedItem != null && (selectedItem.Label == "DiamondWidth" || selectedItem.Label == "DiamondHeight"))
                    {
                        // Allow Enter to be processed normally (it will commit the edit)
                        // Then invalidate the view after a short delay to ensure the value is committed
                        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                        timer.Interval = 10; // Small delay to let PropertyGrid commit
                        timer.Tick += (sender, args) =>
                        {
                            timer.Stop();
                            timer.Dispose();
                            _mapRenderControl?.Invalidate();
                        };
                        timer.Start();
                    }
                }
            };
            
            // Modern styled save button panel
            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = new Padding(10, 8, 10, 8)
            };
            buttonPanel.Paint += (s, e) =>
            {
                // Draw top border
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 0, buttonPanel.Width, 0);
                }
            };
            
            _saveButton = new Button
            {
                Text = "Save Changes",
                Dock = DockStyle.Fill,
                Height = 34,
                Enabled = false,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 120, 215),
                Cursor = Cursors.Hand
            };
            _saveButton.FlatAppearance.BorderSize = 0;
            _saveButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 102, 204);
            _saveButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 80, 160);
            _saveButton.Click += (s, e) => SaveProperties();
            
            buttonPanel.Controls.Add(_saveButton);
            
            // Layout
            // Bounding box preview panel
            Panel previewPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 150,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(5)
            };
            previewPanel.Paint += (s, e) => DrawBoundingBoxPreview(e.Graphics, previewPanel.ClientRectangle);
            
            Label previewLabel = new Label
            {
                Text = "Bounding Box Preview",
                Dock = DockStyle.Top,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            previewPanel.Controls.Add(previewLabel);
            
            // Bounding box color picker panel
            Panel colorPickerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(245, 245, 245),
                Padding = new Padding(10, 8, 10, 8)
            };
            colorPickerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                {
                    e.Graphics.DrawLine(pen, 0, colorPickerPanel.Height - 1, colorPickerPanel.Width, colorPickerPanel.Height - 1);
                }
            };
            
            Label colorLabel = new Label
            {
                Text = "Bounding Box Color:",
                Location = new Point(10, 15),
                Size = new Size(130, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(30, 30, 30)
            };
            
            _colorPickerButton = new Button
            {
                Location = new Point(145, 10),
                Size = new Size(100, 30),
                Text = "Pick Color",
                Font = new Font("Segoe UI", 9),
                BackColor = Color.Cyan,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            _colorPickerButton.FlatAppearance.BorderSize = 1;
            _colorPickerButton.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            _colorPickerButton.Click += (s, e) =>
            {
                if (_currentCamera != null)
                {
                    using (ColorDialog colorDialog = new ColorDialog())
                    {
                        colorDialog.Color = Color.FromArgb(
                            _currentCamera.BoundingBoxColorR,
                            _currentCamera.BoundingBoxColorG,
                            _currentCamera.BoundingBoxColorB
                        );
                        colorDialog.FullOpen = true;
                        
                        if (colorDialog.ShowDialog() == DialogResult.OK)
                        {
                            _currentCamera.BoundingBoxColorR = colorDialog.Color.R;
                            _currentCamera.BoundingBoxColorG = colorDialog.Color.G;
                            _currentCamera.BoundingBoxColorB = colorDialog.Color.B;
                            _colorPickerButton.BackColor = colorDialog.Color;
                            UpdatePropertyGrid();
                            previewPanel.Invalidate();
                            _mapRenderControl?.Invalidate();
                            _onSaveCallback?.Invoke();
                        }
                    }
                }
            };
            
            colorPickerPanel.Controls.Add(colorLabel);
            colorPickerPanel.Controls.Add(_colorPickerButton);
            this.Controls.Add(colorPickerPanel);
            
            this.Controls.Add(_propertyGrid);
            this.Controls.Add(previewPanel);
            this.Controls.Add(buttonPanel);
            this.Controls.Add(titlePanel);
            
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
                // Get mouse position in screen coordinates
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
                // Get current mouse position in screen coordinates
                Point currentMousePos = Control.MousePosition;
                
                // Calculate how far we've dragged
                int deltaX = Math.Abs(currentMousePos.X - _dragStartMousePosition.X);
                int deltaY = Math.Abs(currentMousePos.Y - _dragStartMousePosition.Y);
                
                // If dragged more than 5 pixels, undock
                if (deltaX > 5 || deltaY > 5)
                {
                    _isDragging = false;
                    Undock();
                    
                    // Set new position based on current mouse position
                    if (_parentForm != null)
                    {
                        Point screenPos = Control.MousePosition;
                        Point clientPos = _parentForm.PointToClient(screenPos);
                        // Offset to center the window on the mouse
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

        private void UpdatePropertyGrid()
        {
            if (_currentCamera != null)
            {
                // Update color picker button
                if (_colorPickerButton != null)
                {
                    _colorPickerButton.BackColor = Color.FromArgb(
                        _currentCamera.BoundingBoxColorR,
                        _currentCamera.BoundingBoxColorG,
                        _currentCamera.BoundingBoxColorB
                    );
                }
                
                // Update name text box (temporarily disable TextChanged to avoid triggering save)
                _nameTextBox.TextChanged -= _nameTextBox_TextChanged;
                _nameTextBox.Text = _currentCamera.Name ?? "";
                _nameTextBox.TextChanged += _nameTextBox_TextChanged;
                
                // Create a wrapper to expose properties with categories
                _propertyGrid.SelectedObject = new CameraPropertiesWrapper(_currentCamera);
                UpdateTitle();
                _saveButton.Enabled = false; // Reset save button since we're loading existing data
            }
            else
            {
                _nameTextBox.TextChanged -= _nameTextBox_TextChanged;
                _nameTextBox.Text = "";
                _nameTextBox.TextChanged += _nameTextBox_TextChanged;
                _propertyGrid.SelectedObject = null;
                _titleLabel.Text = "  Camera Properties  •  No Selection";
                _saveButton.Enabled = false;
            }
            // Refresh preview when entity changes
            foreach (Control control in this.Controls)
            {
                if (control is Panel panel && panel.BackColor == Color.FromArgb(30, 30, 30))
                {
                    panel.Invalidate();
                    break;
                }
            }
        }
        
        private void _nameTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (_currentCamera != null)
            {
                _currentCamera.Name = _nameTextBox.Text;
                _saveButton.Enabled = true;
                UpdateTitle();
            }
        }
        
        private void UpdateTitle()
        {
            if (_currentCamera != null)
            {
                string nameDisplay = string.IsNullOrWhiteSpace(_currentCamera.Name) ? "Unnamed" : _currentCamera.Name;
                _titleLabel.Text = $"  Camera Properties  •  {nameDisplay}  •  X: {_currentCamera.X:F1}, Y: {_currentCamera.Y:F1}";
            }
        }

        public void SetSaveCallback(Action callback)
        {
            _onSaveCallback = callback;
        }

        private void SaveProperties()
        {
            if (_currentCamera != null)
            {
                // Force PropertyGrid to commit any pending edits
                _propertyGrid.Refresh();
                
                // Trigger save callback
                _onSaveCallback?.Invoke();
                
                _saveButton.Enabled = false;
                MessageBox.Show("Camera properties saved.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        /// <summary>
        /// Wrapper class to organize camera properties in categories for PropertyGrid
        /// </summary>
        private class CameraPropertiesWrapper
        {
            private CameraData _cameraData;
            
            public CameraPropertiesWrapper(CameraData cameraData)
            {
                _cameraData = cameraData;
            }
            
            // Position (from EnemyData)
            [System.ComponentModel.Category("Position")]
            [System.ComponentModel.Description("X position in pixels")]
            public float X
            {
                get => _cameraData.X;
                set => _cameraData.X = value;
            }
            
            [System.ComponentModel.Category("Position")]
            [System.ComponentModel.Description("Y position in pixels")]
            public float Y
            {
                get => _cameraData.Y;
                set => _cameraData.Y = value;
            }
            
            // Detection (from EnemyData)
            [System.ComponentModel.Category("Detection")]
            [System.ComponentModel.Description("Detection range in pixels")]
            public float DetectionRange
            {
                get => _cameraData.DetectionRange;
                set => _cameraData.DetectionRange = value;
            }
            
            [System.ComponentModel.Category("Detection")]
            [System.ComponentModel.Description("Sight cone angle in degrees")]
            public float SightConeAngle
            {
                get => _cameraData.SightConeAngle;
                set => _cameraData.SightConeAngle = value;
            }
            
            [System.ComponentModel.Category("Detection")]
            [System.ComponentModel.Description("Sight cone length in pixels (-1 means use DetectionRange)")]
            public float CameraSightConeLength
            {
                get => _cameraData.CameraSightConeLength;
                set => _cameraData.CameraSightConeLength = value;
            }
            
            // Rotation
            [System.ComponentModel.Category("Rotation")]
            [System.ComponentModel.Description("Current rotation in radians")]
            public float Rotation
            {
                get => _cameraData.Rotation;
                set => _cameraData.Rotation = value;
            }
            
            [System.ComponentModel.Category("Rotation")]
            [System.ComponentModel.Description("Rotation speed in degrees per second")]
            public float CameraRotationSpeed
            {
                get => _cameraData.CameraRotationSpeed;
                set => _cameraData.CameraRotationSpeed = value;
            }
            
            [System.ComponentModel.Category("Rotation")]
            [System.ComponentModel.Description("Sweep angle in degrees (how far the camera rotates back and forth)")]
            public float SweepAngle
            {
                get => _cameraData.SweepAngle;
                set => _cameraData.SweepAngle = value;
            }
            
            [System.ComponentModel.Category("Rotation")]
            [System.ComponentModel.Description("Pause duration at rotation endpoints in seconds")]
            public float PauseDuration
            {
                get => _cameraData.PauseDuration;
                set => _cameraData.PauseDuration = value;
            }
            
            // Alert
            [System.ComponentModel.Category("Alert")]
            [System.ComponentModel.Description("Radius to alert enemies in pixels")]
            public float AlertRadius
            {
                get => _cameraData.AlertRadius;
                set => _cameraData.AlertRadius = value;
            }
            
            [System.ComponentModel.Category("Alert")]
            [System.ComponentModel.Description("Cooldown between alerts in seconds")]
            public float AlertCooldown
            {
                get => _cameraData.AlertCooldown;
                set => _cameraData.AlertCooldown = value;
            }
            
            // Inherited from EnemyData (expose relevant ones)
            [System.ComponentModel.Category("Enemy Properties")]
            [System.ComponentModel.Description("Attack range (inherited from Enemy, not used by cameras)")]
            public float AttackRange
            {
                get => _cameraData.AttackRange;
                set => _cameraData.AttackRange = value;
            }
            
            [System.ComponentModel.Category("Enemy Properties")]
            [System.ComponentModel.Description("Chase speed (inherited from Enemy, not used by cameras)")]
            public float ChaseSpeed
            {
                get => _cameraData.ChaseSpeed;
                set => _cameraData.ChaseSpeed = value;
            }
            
            [System.ComponentModel.Category("Enemy Properties")]
            [System.ComponentModel.Description("Max health (inherited from Enemy, not used by cameras)")]
            public float MaxHealth
            {
                get => _cameraData.MaxHealth;
                set => _cameraData.MaxHealth = value;
            }
            
            [System.ComponentModel.Category("Visual")]
            [System.ComponentModel.Description("Width of the diamond texture in pixels. Height is automatically adjusted to maintain 2:1 aspect ratio.")]
            public int DiamondWidth
            {
                get => _cameraData.DiamondWidth;
                set
                {
                    _cameraData.DiamondWidth = value;
                    // Maintain isometric 2:1 aspect ratio (width:height)
                    _cameraData.DiamondHeight = value / 2;
                }
            }

            [System.ComponentModel.Category("Visual")]
            [System.ComponentModel.Description("Height of the diamond texture in pixels. Width is automatically adjusted to maintain 2:1 aspect ratio.")]
            public int DiamondHeight
            {
                get => _cameraData.DiamondHeight;
                set
                {
                    _cameraData.DiamondHeight = value;
                    // Maintain isometric 2:1 aspect ratio (width:height)
                    _cameraData.DiamondWidth = value * 2;
                }
            }
            
            [System.ComponentModel.Category("Visual")]
            [System.ComponentModel.Description("Z height for 3D isometric rendering (vertical offset)")]
            public float ZHeight
            {
                get => _cameraData.ZHeight;
                set => _cameraData.ZHeight = Math.Max(0.0f, value);
            }
            
            [System.ComponentModel.Category("Bounding Box")]
            [System.ComponentModel.Description("Bounding box color - Red component (0-255)")]
            public byte BoundingBoxColorR
            {
                get => _cameraData.BoundingBoxColorR;
                set => _cameraData.BoundingBoxColorR = value;
            }
            
            [System.ComponentModel.Category("Bounding Box")]
            [System.ComponentModel.Description("Bounding box color - Green component (0-255)")]
            public byte BoundingBoxColorG
            {
                get => _cameraData.BoundingBoxColorG;
                set => _cameraData.BoundingBoxColorG = value;
            }
            
            [System.ComponentModel.Category("Bounding Box")]
            [System.ComponentModel.Description("Bounding box color - Blue component (0-255)")]
            public byte BoundingBoxColorB
            {
                get => _cameraData.BoundingBoxColorB;
                set => _cameraData.BoundingBoxColorB = value;
            }
            
            [System.ComponentModel.Category("Bounding Box")]
            [System.ComponentModel.Description("Bounding box opacity (0.0 to 1.0, where 0.3 = 30%)")]
            public float BoundingBoxOpacity
            {
                get => _cameraData.BoundingBoxOpacity;
                set => _cameraData.BoundingBoxOpacity = Math.Clamp(value, 0.0f, 1.0f);
            }
        }
        
        private void DrawBoundingBoxPreview(Graphics g, Rectangle bounds)
        {
            if (_currentCamera == null) return;
            
            // Clear background
            g.Clear(Color.FromArgb(30, 30, 30));
            
            // Calculate preview area (leave space for label)
            Rectangle previewArea = new Rectangle(
                bounds.X + 5,
                bounds.Y + 25,
                bounds.Width - 10,
                bounds.Height - 30
            );
            
            // Center of preview
            float centerX = previewArea.X + previewArea.Width / 2.0f;
            float centerY = previewArea.Y + previewArea.Height / 2.0f;
            
            // Get entity dimensions
            float width = _currentCamera.DiamondWidth;
            float height = _currentCamera.DiamondHeight;
            float zHeight = _currentCamera.ZHeight;
            
            // Scale factor to fit the bounding box in the preview
            float maxDimension = Math.Max(width, height);
            float maxScreenHeight = maxDimension + (zHeight > 0 ? zHeight * 0.5f : 0); // Height + Z offset
            float scaleX = previewArea.Width / (maxDimension * 1.5f);
            float scaleY = previewArea.Height / (maxScreenHeight * 1.5f);
            float scale = Math.Min(scaleX, scaleY);
            scale = Math.Max(0.1f, Math.Min(scale, 2.0f)); // Clamp scale
            
            float halfWidth = width / 2.0f;
            float halfHeight = height / 2.0f;
            
            // Get bounding box color from entity data
            Color boxColor = Color.FromArgb(
                _currentCamera.BoundingBoxColorR,
                _currentCamera.BoundingBoxColorG,
                _currentCamera.BoundingBoxColorB
            );
            
            // ZHeight represents the TOP of the object
            // Base is always at z = 0, top is at z = zHeight
            const float heightScale = 0.5f;
            float zOffsetY = zHeight * heightScale;
            
            if (zHeight > 0)
            {
                // Draw 3D bounding box using isometric diamond shape
                // Bottom face corners (z = 0) - base of the object
                PointF bottomTop = new PointF(centerX, centerY - halfHeight * scale);
                PointF bottomRight = new PointF(centerX + halfWidth * scale, centerY);
                PointF bottomBottom = new PointF(centerX, centerY + halfHeight * scale);
                PointF bottomLeft = new PointF(centerX - halfWidth * scale, centerY);
                
                // Top face corners (z = zHeight) - top of the object
                // In isometric, Z height affects the Y coordinate (moves up in screen space)
                PointF topTop = new PointF(centerX, centerY - halfHeight * scale - zOffsetY * scale);
                PointF topRight = new PointF(centerX + halfWidth * scale, centerY - zOffsetY * scale);
                PointF topBottom = new PointF(centerX, centerY + halfHeight * scale - zOffsetY * scale);
                PointF topLeft = new PointF(centerX - halfWidth * scale, centerY - zOffsetY * scale);
                
                // Use entity's opacity for preview
                float previewOpacity = _currentCamera.BoundingBoxOpacity;
                int alpha = (int)(previewOpacity * 255.0f);
                Color fillColor = Color.FromArgb(alpha, boxColor);
                
                // Draw filled faces with semi-transparent color
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
                
                using (Pen boxPen = new Pen(boxColor, 2.0f))
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
            else
            {
                // Draw 2D diamond outline (isometric shape)
                PointF[] diamondPoints = new PointF[]
                {
                    new PointF(centerX, centerY - halfHeight * scale),           // Top
                    new PointF(centerX + halfWidth * scale, centerY),              // Right
                    new PointF(centerX, centerY + halfHeight * scale),            // Bottom
                    new PointF(centerX - halfWidth * scale, centerY)               // Left
                };
                
                // Draw filled diamond with semi-transparent color
                float previewOpacity = _currentCamera.BoundingBoxOpacity;
                int alpha = (int)(previewOpacity * 255.0f);
                Color fillColor = Color.FromArgb(alpha, boxColor);
                using (SolidBrush fillBrush = new SolidBrush(fillColor))
                {
                    g.FillPolygon(fillBrush, diamondPoints);
                }
                
                // Draw outline
                using (Pen boxPen = new Pen(boxColor, 2.0f))
                {
                    g.DrawPolygon(boxPen, diamondPoints);
                }
            }
        }
    }
}
