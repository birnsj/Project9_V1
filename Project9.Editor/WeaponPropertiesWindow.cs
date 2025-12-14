using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Project9.Shared;

namespace Project9.Editor
{
    /// <summary>
    /// Dockable window for editing weapon properties
    /// </summary>
    public class WeaponPropertiesWindow : Form
    {
        private PropertyGrid _propertyGrid = null!;
        private WeaponData? _currentWeapon;
        private Button _dockButton = null!;
        private Label _titleLabel = null!;
        private TextBox _typeTextBox = null!;
        private Label _typeLabel = null!;
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

        public WeaponData? CurrentWeapon
        {
            get => _currentWeapon;
            set
            {
                _currentWeapon = value;
                UpdatePropertyGrid();
            }
        }

        public WeaponPropertiesWindow()
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
            this.Text = "Weapon Properties";
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
                Text = "Weapon Properties",
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

            // Type field panel at the top
            Panel typePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = new Padding(10, 8, 10, 8)
            };
            typePanel.Paint += (s, e) =>
            {
                // Draw bottom border
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                {
                    e.Graphics.DrawLine(pen, 0, typePanel.Height - 1, typePanel.Width, typePanel.Height - 1);
                }
            };
            
            _typeLabel = new Label
            {
                Text = "Type:",
                Location = new Point(10, 12),
                Size = new Size(50, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(30, 30, 30)
            };
            
            _typeTextBox = new TextBox
            {
                Location = new Point(65, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = typePanel.Width - 75,
                Font = new Font("Segoe UI", 9),
                BorderStyle = BorderStyle.FixedSingle
            };
            _typeTextBox.TextChanged += _typeTextBox_TextChanged;
            
            typePanel.Controls.Add(_typeLabel);
            typePanel.Controls.Add(_typeTextBox);
            this.Controls.Add(typePanel);

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
            
            this.Controls.Add(_propertyGrid);
            this.Controls.Add(previewPanel);
            
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
            if (_currentWeapon != null)
            {
                // Update type text box (read-only, shows current type)
                _typeTextBox.TextChanged -= _typeTextBox_TextChanged;
                _typeTextBox.Text = _currentWeapon.Type;
                _typeTextBox.ReadOnly = true; // Type is determined by the class, not editable
                _typeTextBox.TextChanged += _typeTextBox_TextChanged;
                
                // Create a wrapper object for property grid editing
                // Use GunPropertiesWrapper for guns to show gun-specific properties
                object wrapper = _currentWeapon switch
                {
                    GunData gunData => new GunPropertiesWrapper(gunData),
                    _ => new WeaponPropertiesWrapper(_currentWeapon)
                };
                _propertyGrid.SelectedObject = wrapper;
                UpdateTitle();
                // Data loaded
            }
            else
            {
                _typeTextBox.TextChanged -= _typeTextBox_TextChanged;
                _typeTextBox.Text = "";
                _typeTextBox.ReadOnly = false;
                _typeTextBox.TextChanged += _typeTextBox_TextChanged;
                _propertyGrid.SelectedObject = null;
                _titleLabel.Text = "  Weapon Properties  •  No Selection";
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
        
        private void _typeTextBox_TextChanged(object? sender, EventArgs e)
        {
            // Type is read-only, so this shouldn't be called, but handle it gracefully
            if (_currentWeapon != null)
            {
                _onSaveCallback?.Invoke(); // Auto-save on change
                UpdateTitle();
            }
        }
        
        private void UpdateTitle()
        {
            if (_currentWeapon != null)
            {
                string typeDisplay = string.IsNullOrWhiteSpace(_currentWeapon.Type) ? "Unnamed" : _currentWeapon.Type;
                _titleLabel.Text = $"  Weapon Properties  •  {typeDisplay}  •  X: {_currentWeapon.X:F1}, Y: {_currentWeapon.Y:F1}";
            }
        }

        private void PropertyGrid_PropertyValueChanged(object? s, PropertyValueChangedEventArgs e)
        {
            // Auto-save on property change
            _onSaveCallback?.Invoke();
            
            // Update title if Type, X, or Y changed
            if (_currentWeapon != null && (e.ChangedItem?.Label == "Type" || e.ChangedItem?.Label == "X" || e.ChangedItem?.Label == "Y"))
            {
                UpdateTitle();
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
        /// Wrapper class to expose WeaponData properties for PropertyGrid editing
        /// </summary>
        private class WeaponPropertiesWrapper
        {
            private WeaponData _weapon;

            public WeaponPropertiesWrapper(WeaponData weapon)
            {
                _weapon = weapon;
            }

            [System.ComponentModel.Category("Position")]
            [System.ComponentModel.Description("X coordinate in world space (pixels)")]
            public float X
            {
                get => _weapon.X;
                set => _weapon.X = value;
            }

            [System.ComponentModel.Category("Position")]
            [System.ComponentModel.Description("Y coordinate in world space (pixels)")]
            public float Y
            {
                get => _weapon.Y;
                set => _weapon.Y = value;
            }

            [System.ComponentModel.Category("Weapon")]
            [System.ComponentModel.Description("Weapon type (read-only, determined by weapon class)")]
            [System.ComponentModel.ReadOnly(true)]
            public string Type
            {
                get => _weapon.Type;
            }
            
            [System.ComponentModel.Category("Weapon")]
            [System.ComponentModel.Description("Weapon name")]
            public string Name
            {
                get => _weapon.Name;
                set => _weapon.Name = value;
            }
            
            [System.ComponentModel.Category("Combat")]
            [System.ComponentModel.Description("Damage dealt by this weapon")]
            public float Damage
            {
                get => _weapon.Damage;
                set => _weapon.Damage = value;
            }
            
            [System.ComponentModel.Category("Combat")]
            [System.ComponentModel.Description("Knockback/stun duration in seconds when enemy is hit")]
            public float KnockbackDuration
            {
                get => _weapon.KnockbackDuration;
                set => _weapon.KnockbackDuration = value;
            }
            
            [System.ComponentModel.Category("Visual")]
            [System.ComponentModel.Description("Weapon color red component (0-255)")]
            public int WeaponColorR
            {
                get => _weapon.WeaponColorR;
                set => _weapon.WeaponColorR = Math.Clamp(value, 0, 255);
            }
            
            [System.ComponentModel.Category("Visual")]
            [System.ComponentModel.Description("Weapon color green component (0-255)")]
            public int WeaponColorG
            {
                get => _weapon.WeaponColorG;
                set => _weapon.WeaponColorG = Math.Clamp(value, 0, 255);
            }
            
            [System.ComponentModel.Category("Visual")]
            [System.ComponentModel.Description("Weapon color blue component (0-255)")]
            public int WeaponColorB
            {
                get => _weapon.WeaponColorB;
                set => _weapon.WeaponColorB = Math.Clamp(value, 0, 255);
            }
            
            // Gun-specific properties (only shown for GunData)
            [System.ComponentModel.Category("Gun Properties")]
            [System.ComponentModel.Description("Projectile speed in pixels per second (Gun only)")]
            [System.ComponentModel.Browsable(false)] // Hidden by default, shown via GunPropertiesWrapper
            public float? ProjectileSpeed
            {
                get => _weapon is GunData gunData ? gunData.ProjectileSpeed : null;
                set
                {
                    if (_weapon is GunData gunData && value.HasValue)
                    {
                        gunData.ProjectileSpeed = value.Value;
                    }
                }
            }
            
            [System.ComponentModel.Category("Gun Properties")]
            [System.ComponentModel.Description("Fire rate in shots per second (Gun only)")]
            [System.ComponentModel.Browsable(false)] // Hidden by default, shown via GunPropertiesWrapper
            public float? FireRate
            {
                get => _weapon is GunData gunData ? gunData.FireRate : null;
                set
                {
                    if (_weapon is GunData gunData && value.HasValue)
                    {
                        gunData.FireRate = value.Value;
                    }
                }
            }
            
            [System.ComponentModel.Category("Visual")]
            [System.ComponentModel.Description("Z height for 3D isometric rendering (vertical offset)")]
            public float ZHeight
            {
                get => _weapon.ZHeight;
                set => _weapon.ZHeight = Math.Max(0.0f, value);
            }
        }
        
        /// <summary>
        /// Wrapper class specifically for GunData to show gun-specific properties
        /// </summary>
        private class GunPropertiesWrapper : WeaponPropertiesWrapper
        {
            private GunData _gunData;
            
            public GunPropertiesWrapper(GunData gunData) : base(gunData)
            {
                _gunData = gunData;
            }
            
            [System.ComponentModel.Category("Gun Properties")]
            [System.ComponentModel.Description("Projectile speed in pixels per second")]
            public new float ProjectileSpeed
            {
                get => _gunData.ProjectileSpeed;
                set => _gunData.ProjectileSpeed = value;
            }
            
            [System.ComponentModel.Category("Gun Properties")]
            [System.ComponentModel.Description("Fire rate in shots per second")]
            public new float FireRate
            {
                get => _gunData.FireRate;
                set => _gunData.FireRate = value;
            }
        }
        
        private void DrawBoundingBoxPreview(Graphics g, Rectangle bounds)
        {
            if (_currentWeapon == null) return;
            
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
            
            // Weapons don't have diamond dimensions, use defaults
            float width = 24.0f; // Default weapon width (matches MapRenderControl)
            float height = 12.0f; // Default weapon height (matches MapRenderControl)
            float zHeight = _currentWeapon.ZHeight;
            
            // Scale factor to fit the bounding box in the preview
            float maxDimension = Math.Max(width, height);
            float maxScreenHeight = maxDimension + (zHeight > 0 ? zHeight * 0.5f : 0); // Height + Z offset
            float scaleX = previewArea.Width / (maxDimension * 1.5f);
            float scaleY = previewArea.Height / (maxScreenHeight * 1.5f);
            float scale = Math.Min(scaleX, scaleY);
            scale = Math.Max(0.1f, Math.Min(scale, 2.0f)); // Clamp scale
            
            float halfWidth = width / 2.0f;
            float halfHeight = height / 2.0f;
            
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
                
                using (Pen boxPen = new Pen(Color.Cyan, 2.0f))
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
                
                using (Pen boxPen = new Pen(Color.Cyan, 2.0f))
                {
                    g.DrawPolygon(boxPen, diamondPoints);
                }
            }
        }
    }
}

