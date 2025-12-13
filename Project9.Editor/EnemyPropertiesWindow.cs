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
        private Button _saveButton = null!;
        private Button _dockButton = null!;
        private Label _titleLabel = null!;
        private TextBox _nameTextBox = null!;
        private Label _nameLabel = null!;
        private Action? _onSaveCallback;
        private bool _isDocked = false;
        private Form? _parentForm;
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
        
        public bool IsDocked => _isDocked;
        
        public int DockedHeight => _isDocked ? this.Height : 0;
        
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
            this.Controls.Add(_propertyGrid);

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
            _saveButton.Click += SaveButton_Click;
            
            buttonPanel.Controls.Add(_saveButton);
            this.Controls.Add(buttonPanel);

            // Set control order (bottom to top)
            this.Controls.SetChildIndex(buttonPanel, 0);
            this.Controls.SetChildIndex(_propertyGrid, 1);
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
                _saveButton.Enabled = false; // Reset save button since we're loading existing data
            }
            else
            {
                _nameTextBox.TextChanged -= _nameTextBox_TextChanged;
                _nameTextBox.Text = "";
                _nameTextBox.TextChanged += _nameTextBox_TextChanged;
                _propertyGrid.SelectedObject = null;
                _titleLabel.Text = "  Enemy Properties  •  No Selection";
                _saveButton.Enabled = false;
            }
        }
        
        private void _nameTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (_currentEnemy != null)
            {
                _currentEnemy.Name = _nameTextBox.Text;
                _saveButton.Enabled = true;
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
            // Property changed - enable save button
            _saveButton.Enabled = true;
            
            // Update title if Name, X, or Y changed
            if (_currentEnemy != null && (e.ChangedItem?.Label == "Name" || e.ChangedItem?.Label == "X" || e.ChangedItem?.Label == "Y"))
            {
                UpdateTitle();
            }
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            // Trigger save callback
            _onSaveCallback?.Invoke();
            _saveButton.Enabled = false;
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

