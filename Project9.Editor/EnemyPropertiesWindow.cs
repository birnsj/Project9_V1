using System;
using System.Drawing;
using System.Windows.Forms;
using Project9.Shared;

namespace Project9.Editor
{
    /// <summary>
    /// Dockable window for editing enemy properties
    /// </summary>
    public class EnemyPropertiesWindow : Form
    {
        private PropertyGrid _propertyGrid = null!;
        private EnemyData? _currentEnemy;
        private Button _dockButton = null!;
        private Label _titleLabel = null!;
        private TextBox _nameTextBox = null!;
        private Label _nameLabel = null!;
        private Action? _onSaveCallback;
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

        public EnemyData? CurrentEnemy
        {
            get => _currentEnemy;
            set
            {
                _currentEnemy = value;
                UpdatePropertyGrid();
            }
        }

        public EnemyPropertiesWindow()
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
                // Find splitter that comes before this window in z-order
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
                    int newWindowHeight = Math.Max(200, panelHeight / remainingWindows);
                    foreach (Control control in dockPanel.Controls)
                    {
                        if (control is Form form && form.Visible)
                        {
                            form.Height = newWindowHeight;
                        }
                    }
                    
                    // Show the first available window (they should all be visible now)
                    foreach (Control control in dockPanel.Controls)
                    {
                        if (control is Form otherForm)
                        {
                            otherForm.Visible = true;
                            break;
                        }
                    }
                }
                
                // Resume dock panel layout
                dockPanel.ResumeLayout(false);
                
                if (dockPanel.Controls.Count == 0)
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
            this.Text = "Enemy Properties";
            this.Size = new Size(350, 500);
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
                Text = "Enemy Properties",
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
            this.Controls.Add(titlePanel);

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
            _propertyGrid.PropertyValueChanged += PropertyGrid_PropertyValueChanged;
            
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
            
            this.Controls.Add(_propertyGrid);
            this.Controls.Add(bottomPanel);
            
            // Enable drag-to-adjust for numeric properties
            var dragHandler = new PropertyGridDragHandler(_propertyGrid, () =>
            {
                _onSaveCallback?.Invoke(); // Auto-save on change
                _mapRenderControl?.Invalidate(); // Refresh the editor view
            });

            // Set control order
            this.Controls.SetChildIndex(_propertyGrid, 0);
            // Title panel is already at index 2
            
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
                    
                    // Undock first
                    Undock();
                    
                    // Wait a moment for Windows to process the undocking
                    Application.DoEvents();
                    
                    // Set new position based on current mouse position (screen coordinates)
                    Point screenPos = Control.MousePosition;
                    // Offset to center the window on the mouse
                    this.Location = new Point(
                        screenPos.X - this.Width / 2,
                        screenPos.Y - 10
                    );
                    this.BringToFront();
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
            if (_currentEnemy != null)
            {
                // Update name text box (temporarily disable TextChanged to avoid triggering save)
                _nameTextBox.TextChanged -= _nameTextBox_TextChanged;
                _nameTextBox.Text = _currentEnemy.Name ?? "";
                _nameTextBox.TextChanged += _nameTextBox_TextChanged;
                
                // Create a wrapper object for property grid editing
                var wrapper = new EnemyPropertiesWrapper(_currentEnemy);
                _propertyGrid.SelectedObject = wrapper;
                UpdateTitle();
                // Data loaded
            }
            else
            {
                _nameTextBox.TextChanged -= _nameTextBox_TextChanged;
                _nameTextBox.Text = "";
                _nameTextBox.TextChanged += _nameTextBox_TextChanged;
                _propertyGrid.SelectedObject = null;
                _titleLabel.Text = "  Enemy Properties  •  No Selection";
            }
        }
        
        private void _nameTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (_currentEnemy != null)
            {
                _currentEnemy.Name = _nameTextBox.Text;
                _onSaveCallback?.Invoke(); // Auto-save on change
                UpdateTitle();
            }
        }
        
        private void UpdateTitle()
        {
            if (_currentEnemy != null)
            {
                string nameDisplay = string.IsNullOrWhiteSpace(_currentEnemy.Name) ? "Unnamed" : _currentEnemy.Name;
                _titleLabel.Text = $"  Enemy Properties  •  {nameDisplay}  •  X: {_currentEnemy.X:F1}, Y: {_currentEnemy.Y:F1}";
            }
        }

        private void PropertyGrid_PropertyValueChanged(object? s, PropertyValueChangedEventArgs e)
        {
            // Auto-save on property change
            _onSaveCallback?.Invoke();
            
            // Update title if Name, X, or Y changed
            if (_currentEnemy != null && (e.ChangedItem?.Label == "Name" || e.ChangedItem?.Label == "X" || e.ChangedItem?.Label == "Y"))
            {
                UpdateTitle();
            }
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
        /// Wrapper class to expose EnemyData properties for PropertyGrid editing
        /// </summary>
        private class EnemyPropertiesWrapper
        {
            private EnemyData _enemy;

            public EnemyPropertiesWrapper(EnemyData enemy)
            {
                _enemy = enemy;
            }

            [System.ComponentModel.Category("Position")]
            [System.ComponentModel.Description("X coordinate in world space (pixels)")]
            public float X
            {
                get => _enemy.X;
                set => _enemy.X = value;
            }

            [System.ComponentModel.Category("Position")]
            [System.ComponentModel.Description("Y coordinate in world space (pixels)")]
            public float Y
            {
                get => _enemy.Y;
                set => _enemy.Y = value;
            }

            [System.ComponentModel.Category("Combat")]
            [System.ComponentModel.Description("Attack range in pixels")]
            public float AttackRange
            {
                get => _enemy.AttackRange;
                set => _enemy.AttackRange = value;
            }

            [System.ComponentModel.Category("Combat")]
            [System.ComponentModel.Description("Attack cooldown in seconds")]
            public float AttackCooldown
            {
                get => _enemy.AttackCooldown;
                set => _enemy.AttackCooldown = value;
            }

            [System.ComponentModel.Category("Combat")]
            [System.ComponentModel.Description("Maximum health")]
            public float MaxHealth
            {
                get => _enemy.MaxHealth;
                set => _enemy.MaxHealth = value;
            }

            [System.ComponentModel.Category("Detection")]
            [System.ComponentModel.Description("Detection range (aggro radius) in pixels")]
            public float DetectionRange
            {
                get => _enemy.DetectionRange;
                set => _enemy.DetectionRange = value;
            }

            [System.ComponentModel.Category("Detection")]
            [System.ComponentModel.Description("Sight cone angle in degrees")]
            public float SightConeAngle
            {
                get => _enemy.SightConeAngle;
                set => _enemy.SightConeAngle = value;
            }

            [System.ComponentModel.Category("Detection")]
            [System.ComponentModel.Description("Sight cone length in pixels (-1 to use DetectionRange * 0.8)")]
            public float SightConeLength
            {
                get => _enemy.SightConeLength;
                set => _enemy.SightConeLength = value;
            }

            [System.ComponentModel.Category("Movement")]
            [System.ComponentModel.Description("Chase speed in pixels per second")]
            public float ChaseSpeed
            {
                get => _enemy.ChaseSpeed;
                set => _enemy.ChaseSpeed = value;
            }

            [System.ComponentModel.Category("Movement")]
            [System.ComponentModel.Description("Maximum chase range in pixels")]
            public float MaxChaseRange
            {
                get => _enemy.MaxChaseRange;
                set => _enemy.MaxChaseRange = value;
            }

            [System.ComponentModel.Category("Behavior")]
            [System.ComponentModel.Description("Rotation speed in degrees per second")]
            public float RotationSpeed
            {
                get => _enemy.RotationSpeed;
                set => _enemy.RotationSpeed = value;
            }

            [System.ComponentModel.Category("Behavior")]
            [System.ComponentModel.Description("Initial rotation in radians (-1 for random)")]
            public float InitialRotation
            {
                get => _enemy.InitialRotation;
                set => _enemy.InitialRotation = value;
            }

            [System.ComponentModel.Category("Behavior")]
            [System.ComponentModel.Description("Exclamation mark display duration in seconds")]
            public float ExclamationDuration
            {
                get => _enemy.ExclamationDuration;
                set => _enemy.ExclamationDuration = value;
            }

            [System.ComponentModel.Category("Behavior")]
            [System.ComponentModel.Description("Stop chasing after this many seconds out of range")]
            public float OutOfRangeThreshold
            {
                get => _enemy.OutOfRangeThreshold;
                set => _enemy.OutOfRangeThreshold = value;
            }

            [System.ComponentModel.Category("Search")]
            [System.ComponentModel.Description("Search duration in seconds when player goes out of view")]
            public float SearchDuration
            {
                get => _enemy.SearchDuration;
                set => _enemy.SearchDuration = value;
            }

            [System.ComponentModel.Category("Search")]
            [System.ComponentModel.Description("Search radius in pixels")]
            public float SearchRadius
            {
                get => _enemy.SearchRadius;
                set => _enemy.SearchRadius = value;
            }
        }
    }
}

