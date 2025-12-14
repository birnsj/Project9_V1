using System;
using System.Drawing;
using System.Windows.Forms;

namespace Project9.Editor
{
    /// <summary>
    /// Dialog for adjusting bounding box opacity
    /// </summary>
    public class BoundingBoxOpacityDialog : Form
    {
        private TrackBar _opacitySlider = null!;
        private Label _valueLabel = null!;
        private MapRenderControl? _mapRenderControl;
        
        public BoundingBoxOpacityDialog()
        {
            InitializeComponent();
        }
        
        public void SetMapRenderControl(MapRenderControl mapRenderControl)
        {
            _mapRenderControl = mapRenderControl;
            if (_mapRenderControl != null && _opacitySlider != null)
            {
                _opacitySlider.Value = (int)(_mapRenderControl.BoundingBoxOpacity * 100);
                UpdateValueLabel();
            }
        }
        
        private void InitializeComponent()
        {
            this.Text = "Bounding Box Opacity";
            this.Size = new Size(320, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Padding = new Padding(20, 20, 20, 20);
            
            // Title label
            Label titleLabel = new Label
            {
                Text = "Adjust Bounding Box Opacity",
                Location = new Point(0, 0),
                Size = new Size(280, 25),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30)
            };
            
            // Value label
            _valueLabel = new Label
            {
                Text = "30%",
                Location = new Point(0, 30),
                Size = new Size(280, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(50, 50, 50),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            // Opacity slider
            _opacitySlider = new TrackBar
            {
                Location = new Point(0, 55),
                Size = new Size(280, 45),
                Minimum = 0,
                Maximum = 100,
                Value = 30,
                TickFrequency = 10,
                LargeChange = 10,
                SmallChange = 1
            };
            _opacitySlider.ValueChanged += OpacitySlider_ValueChanged;
            
            // OK button
            Button okButton = new Button
            {
                Text = "OK",
                Location = new Point(200, 105),
                Size = new Size(80, 30),
                DialogResult = DialogResult.OK,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            okButton.FlatAppearance.BorderSize = 0;
            okButton.Click += (s, e) => this.Close();
            
            this.Controls.Add(titleLabel);
            this.Controls.Add(_valueLabel);
            this.Controls.Add(_opacitySlider);
            this.Controls.Add(okButton);
            this.AcceptButton = okButton;
        }
        
        private void OpacitySlider_ValueChanged(object? sender, EventArgs e)
        {
            if (_mapRenderControl != null && _opacitySlider != null)
            {
                float opacity = _opacitySlider.Value / 100.0f;
                _mapRenderControl.BoundingBoxOpacity = opacity;
                UpdateValueLabel();
            }
        }
        
        private void UpdateValueLabel()
        {
            if (_opacitySlider != null && _valueLabel != null)
            {
                _valueLabel.Text = $"{_opacitySlider.Value}%";
            }
        }
    }
}







