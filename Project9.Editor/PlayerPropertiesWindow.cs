using System;
using System.Drawing;
using System.Windows.Forms;
using Project9.Shared;

namespace Project9.Editor
{
    /// <summary>
    /// Dockable window for editing player properties
    /// </summary>
    public class PlayerPropertiesWindow : Form
    {
        private PropertyGrid _propertyGrid = null!;
        private PlayerData? _currentPlayer;
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

        public PlayerData? CurrentPlayer
        {
            get => _currentPlayer;
            set
            {
                _currentPlayer = value;
                UpdatePropertyGrid();
            }
        }

        public PlayerPropertiesWindow()
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
            this.Text = "Player Properties";
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
                Text = "Player Properties",
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
                if (_currentPlayer != null && (e.ChangedItem?.Label == "Name" || e.ChangedItem?.Label == "X" || e.ChangedItem?.Label == "Y"))
                {
                    UpdateTitle();
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
            this.Controls.Add(_propertyGrid);
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
            if (_currentPlayer != null)
            {
                // Update name text box (temporarily disable TextChanged to avoid triggering save)
                _nameTextBox.TextChanged -= _nameTextBox_TextChanged;
                _nameTextBox.Text = _currentPlayer.Name ?? "";
                _nameTextBox.TextChanged += _nameTextBox_TextChanged;
                
                // Create a wrapper to expose properties with categories
                _propertyGrid.SelectedObject = new PlayerPropertiesWrapper(_currentPlayer);
                UpdateTitle();
                _saveButton.Enabled = false; // Reset save button since we're loading existing data
            }
            else
            {
                _nameTextBox.TextChanged -= _nameTextBox_TextChanged;
                _nameTextBox.Text = "";
                _nameTextBox.TextChanged += _nameTextBox_TextChanged;
                _propertyGrid.SelectedObject = null;
                _titleLabel.Text = "  Player Properties  •  No Selection";
                _saveButton.Enabled = false;
            }
        }
        
        private void _nameTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (_currentPlayer != null)
            {
                _currentPlayer.Name = _nameTextBox.Text;
                _saveButton.Enabled = true;
                UpdateTitle();
            }
        }
        
        private void UpdateTitle()
        {
            if (_currentPlayer != null)
            {
                string nameDisplay = string.IsNullOrWhiteSpace(_currentPlayer.Name) ? "Unnamed" : _currentPlayer.Name;
                _titleLabel.Text = $"  Player Properties  •  {nameDisplay}  •  X: {_currentPlayer.X:F1}, Y: {_currentPlayer.Y:F1}";
            }
        }

        public void SetSaveCallback(Action callback)
        {
            _onSaveCallback = callback;
        }

        private void SaveProperties()
        {
            if (_currentPlayer != null)
            {
                // Force PropertyGrid to commit any pending edits
                _propertyGrid.Refresh();
                
                // Trigger save callback
                _onSaveCallback?.Invoke();
                
                _saveButton.Enabled = false;
                MessageBox.Show("Player properties saved.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        /// <summary>
        /// Wrapper class to organize player properties in categories for PropertyGrid
        /// </summary>
        private class PlayerPropertiesWrapper
        {
            private PlayerData _playerData;
            
            public PlayerPropertiesWrapper(PlayerData playerData)
            {
                _playerData = playerData;
            }
            
            [System.ComponentModel.Category("Position")]
            [System.ComponentModel.Description("X position in pixels")]
            public float X
            {
                get => _playerData.X;
                set => _playerData.X = value;
            }
            
            [System.ComponentModel.Category("Position")]
            [System.ComponentModel.Description("Y position in pixels")]
            public float Y
            {
                get => _playerData.Y;
                set => _playerData.Y = value;
            }
            
            [System.ComponentModel.Category("Movement")]
            [System.ComponentModel.Description("Walking speed in pixels per second")]
            public float WalkSpeed
            {
                get => _playerData.WalkSpeed;
                set => _playerData.WalkSpeed = value;
            }
            
            [System.ComponentModel.Category("Movement")]
            [System.ComponentModel.Description("Running speed in pixels per second")]
            public float RunSpeed
            {
                get => _playerData.RunSpeed;
                set => _playerData.RunSpeed = value;
            }
            
            [System.ComponentModel.Category("Movement")]
            [System.ComponentModel.Description("Sneak speed multiplier (applied to walk speed)")]
            public float SneakSpeedMultiplier
            {
                get => _playerData.SneakSpeedMultiplier;
                set => _playerData.SneakSpeedMultiplier = value;
            }
            
            [System.ComponentModel.Category("Movement")]
            [System.ComponentModel.Description("Distance threshold for reaching target (pixels)")]
            public float StopThreshold
            {
                get => _playerData.StopThreshold;
                set => _playerData.StopThreshold = value;
            }
            
            [System.ComponentModel.Category("Movement")]
            [System.ComponentModel.Description("Distance at which player starts slowing down (pixels)")]
            public float SlowdownRadius
            {
                get => _playerData.SlowdownRadius;
                set => _playerData.SlowdownRadius = value;
            }
            
            [System.ComponentModel.Category("Movement")]
            [System.ComponentModel.Description("Distance threshold for final target when sneaking (pixels)")]
            public float SneakStopThreshold
            {
                get => _playerData.SneakStopThreshold;
                set => _playerData.SneakStopThreshold = value;
            }
            
            [System.ComponentModel.Category("Movement")]
            [System.ComponentModel.Description("Distance threshold for final target when running (pixels)")]
            public float RunStopThreshold
            {
                get => _playerData.RunStopThreshold;
                set => _playerData.RunStopThreshold = value;
            }
            
            [System.ComponentModel.Category("Combat")]
            [System.ComponentModel.Description("Damage dealt by player per attack")]
            public float AttackDamage
            {
                get => _playerData.AttackDamage;
                set => _playerData.AttackDamage = value;
            }
            
            [System.ComponentModel.Category("Health")]
            [System.ComponentModel.Description("Maximum health")]
            public float MaxHealth
            {
                get => _playerData.MaxHealth;
                set => _playerData.MaxHealth = value;
            }
            
            [System.ComponentModel.Category("Respawn")]
            [System.ComponentModel.Description("Respawn countdown duration in seconds")]
            public float RespawnCountdown
            {
                get => _playerData.RespawnCountdown;
                set => _playerData.RespawnCountdown = value;
            }
            
            [System.ComponentModel.Category("Visual")]
            [System.ComponentModel.Description("Death pulse speed (pulses per second)")]
            public float DeathPulseSpeed
            {
                get => _playerData.DeathPulseSpeed;
                set => _playerData.DeathPulseSpeed = value;
            }
            
            [System.ComponentModel.Category("Visual")]
            [System.ComponentModel.Description("Current rotation in radians")]
            public float Rotation
            {
                get => _playerData.Rotation;
                set => _playerData.Rotation = value;
            }
        }
    }
}

