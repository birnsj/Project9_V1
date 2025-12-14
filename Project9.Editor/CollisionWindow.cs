using System;
using System.Drawing;
using System.Windows.Forms;

namespace Project9.Editor
{
    /// <summary>
    /// Window for managing collision mode and collision-related options
    /// </summary>
    public class CollisionWindow : Form
    {
        private Button _deleteAllCollisionButton = null!;
        private Label _instructionsLabel = null!;
        private CheckBox _showGridCheckBox = null!;
        private MapRenderControl? _mapRenderControl;
        private bool _originalGridState = false;
        
        public CollisionWindow()
        {
            InitializeComponent();
        }
        
        public void SetMapRenderControl(MapRenderControl mapRenderControl)
        {
            _mapRenderControl = mapRenderControl;
            // Automatically enable collision mode when window is shown
            if (_mapRenderControl != null)
            {
                _mapRenderControl.CollisionMode = true;
                // Store original grid state
                _originalGridState = _mapRenderControl.ShowGrid64x32;
            }
        }
        
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Ensure collision mode is enabled when window is shown
            if (_mapRenderControl != null)
            {
                _mapRenderControl.CollisionMode = true;
                // Turn on the 64x32 grid when window is shown
                _mapRenderControl.ShowGrid64x32 = true;
                if (_showGridCheckBox != null)
                {
                    _showGridCheckBox.Checked = true;
                }
            }
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Hide instead of close to preserve the window
            if (e.CloseReason == CloseReason.UserClosing || e.CloseReason == CloseReason.None)
            {
                e.Cancel = true;
                // Turn off the grid when window is closed/hidden
                if (_mapRenderControl != null)
                {
                    _mapRenderControl.ShowGrid64x32 = _originalGridState;
                }
                this.Hide();
            }
            else if (e.CloseReason == CloseReason.ApplicationExitCall || e.CloseReason == CloseReason.WindowsShutDown)
            {
                // Only disable collision mode and restore grid on actual application exit
                if (_mapRenderControl != null)
                {
                    _mapRenderControl.CollisionMode = false;
                    _mapRenderControl.ShowGrid64x32 = _originalGridState;
                }
            }
            base.OnFormClosing(e);
        }
        
        private void InitializeComponent()
        {
            this.Text = "Collision Mode";
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 240, 240);
            this.AutoSize = false;
            this.Padding = new Padding(0);
            
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
            
            Label titleLabel = new Label
            {
                Text = "Collision Mode",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            
            titlePanel.Controls.Add(titleLabel);
            
            // Main content panel with better styling
            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 16, 16, 16),
                BackColor = Color.White
            };
            
            // Instructions panel with border
            Panel instructionsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110,
                BackColor = Color.FromArgb(248, 248, 248),
                Padding = new Padding(12, 10, 12, 10)
            };
            instructionsPanel.Paint += (s, e) =>
            {
                // Draw border
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, instructionsPanel.Width - 1, instructionsPanel.Height - 1);
                }
            };
            
            // Instructions label
            _instructionsLabel = new Label
            {
                Text = "Collision Mode is now active. The collision grid is visible on the map.\n\n" +
                       "• Left-click on the grid to place collision cells\n" +
                       "• Right-click on collision cells to remove them\n" +
                       "• Cells are placed on the 64x32 diamond grid",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.75f),
                ForeColor = Color.FromArgb(50, 50, 50),
                BackColor = Color.Transparent
            };
            
            instructionsPanel.Controls.Add(_instructionsLabel);
            contentPanel.Controls.Add(instructionsPanel);
            
            // Options panel for checkbox and button
            Panel optionsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                Padding = new Padding(12, 16, 12, 12),
                BackColor = Color.White
            };
            
            // Show 64x32 Grid checkbox
            _showGridCheckBox = new CheckBox
            {
                Text = "Show 64x32 Grid",
                AutoSize = true,
                Location = new Point(0, 0),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(30, 30, 30),
                Checked = true
            };
            _showGridCheckBox.CheckedChanged += ShowGridCheckBox_CheckedChanged;
            
            // Delete All Collision button (smaller)
            _deleteAllCollisionButton = new Button
            {
                Text = "Delete All Collision",
                Size = new Size(160, 28),
                Location = new Point(0, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(220, 53, 69),
                Cursor = Cursors.Hand
            };
            _deleteAllCollisionButton.FlatAppearance.BorderSize = 0;
            _deleteAllCollisionButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 35, 51);
            _deleteAllCollisionButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 25, 41);
            _deleteAllCollisionButton.Click += DeleteAllCollisionButton_Click;
            
            optionsPanel.Controls.Add(_showGridCheckBox);
            optionsPanel.Controls.Add(_deleteAllCollisionButton);
            contentPanel.Controls.Add(optionsPanel);
            
            // Calculate and set window size to fit content nicely
            int contentWidth = 320;
            int contentPadding = contentPanel.Padding.Top + contentPanel.Padding.Bottom;
            int totalHeight = titlePanel.Height + instructionsPanel.Height + optionsPanel.Height + contentPadding;
            this.Size = new Size(contentWidth, totalHeight);
            this.MinimumSize = new Size(contentWidth, totalHeight);
            
            this.Controls.Add(contentPanel);
            this.Controls.Add(titlePanel);
        }
        
        private void ShowGridCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (_mapRenderControl != null && _showGridCheckBox != null)
            {
                _mapRenderControl.ShowGrid64x32 = _showGridCheckBox.Checked;
            }
        }
        
        private void DeleteAllCollisionButton_Click(object? sender, EventArgs e)
        {
            // Confirm deletion
            DialogResult result = MessageBox.Show(
                "Are you sure you want to delete all collision cells? This action cannot be undone.",
                "Delete All Collision Cells",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result == DialogResult.Yes)
            {
                if (_mapRenderControl != null)
                {
                    _mapRenderControl.ClearAllCollisionCells();
                    MessageBox.Show("All collision cells have been deleted.", "Delete All Collision", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        
    }
}

