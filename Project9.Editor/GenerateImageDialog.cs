using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Project9.Editor
{
    /// <summary>
    /// Dialog for generating images using Pollinations AI
    /// </summary>
    public class GenerateImageDialog : Form
    {
        private TextBox _promptTextBox = null!;
        private ComboBox _examplePromptComboBox = null!;
        private ComboBox _generationStyleComboBox = null!;
        private Button _generateButton = null!;
        private Button _retryButton = null!;
        private Button _loadButton = null!;
        private PictureBox _imagePreview = null!;
        private Panel _imagePanel = null!;
        private Label _statusLabel = null!;
        private ProgressBar _progressBar = null!;
        private Label _connectionStatusLabel = null!;
        private Label _tileInfoLabel = null!;
        private Label _tileDimensionsLabel = null!;
        private Label _tileAspectRatioLabel = null!;
        private Label _tileFormatLabel = null!;
        private Label _tileTemplateLabel = null!;
        private HttpClient _httpClient = null!;
        private Image? _originalImage = null;
        private float _zoomLevel = 1.0f;
        private const float ZOOM_MIN = 0.25f;
        private const float ZOOM_MAX = 4.0f;
        private const float ZOOM_STEP = 0.1f;
        
        public GenerateImageDialog()
        {
            InitializeComponent();
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            UpdateConnectionStatus(false, "Ready");
        }
        
        
        private void InitializeComponent()
        {
            this.Text = "Generate Image - Isometric Tile Generator";
            // Window size: increased height to accommodate tile info section and better spacing
            this.Size = new Size(760, 1050);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(245, 245, 250);
            this.MinimumSize = new Size(760, 1050);
            this.Padding = new Padding(20, 20, 20, 20);
            
            int currentY = 0;
            const int spacing = 16;
            const int sectionSpacing = 28;
            
            // Title label
            Label titleLabel = new Label
            {
                Text = "Generate Image with Pollinations AI",
                Location = new Point(0, currentY),
                Size = new Size(600, 28),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30)
            };
            currentY += 35;
            
            // Prompt section
            Label promptLabel = new Label
            {
                Text = "Prompt:",
                Location = new Point(0, currentY),
                Size = new Size(100, 22),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            currentY += 22;
            
            // Prompt text box (multiline, bigger)
            _promptTextBox = new TextBox
            {
                Location = new Point(0, currentY),
                Size = new Size(720, 100),
                Font = new Font("Segoe UI", 9),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = "",
                BorderStyle = BorderStyle.FixedSingle
            };
            currentY += 110 + spacing;
            
            // Example prompts dropdown
            Label exampleLabel = new Label
            {
                Text = "Example Prompts:",
                Location = new Point(0, currentY),
                Size = new Size(140, 22),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            currentY += 22;
            
            _examplePromptComboBox = new ComboBox
            {
                Location = new Point(0, currentY),
                Size = new Size(720, 25),
                Font = new Font("Segoe UI", 9),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat
            };
            currentY += 30 + spacing;
            
            // Add example prompts (include 2:1 isometric perspective and seamless on all 4 sides requirements)
            _examplePromptComboBox.Items.AddRange(new object[]
            {
                "(Select an example or type your own)",
                "lush green grass with small flowers, 2:1 isometric top-down view, seamlessly tileable on all 4 sides",
                "dark stone with cracks and texture, 2:1 isometric perspective, seamlessly tileable on all 4 sides",
                "sandy beach with small rocks, 2:1 isometric top-down view, seamlessly tileable on all 4 sides",
                "forest floor with fallen leaves and moss, 2:1 isometric perspective, seamlessly tileable on all 4 sides",
                "dirt path with footprints, 2:1 isometric top-down view, seamlessly tileable on all 4 sides",
                "water with ripples, 2:1 isometric perspective, seamlessly tileable on all 4 sides",
                "mossy cobblestone, 2:1 isometric top-down view, seamlessly tileable on all 4 sides",
                "dry cracked earth, 2:1 isometric perspective, seamlessly tileable on all 4 sides",
                "snow covered ground, 2:1 isometric top-down view, seamlessly tileable on all 4 sides",
                "muddy ground with puddles, 2:1 isometric perspective, seamlessly tileable on all 4 sides",
                "gravel path, 2:1 isometric top-down view, seamlessly tileable on all 4 sides",
                "brick floor, 2:1 isometric perspective, seamlessly tileable on all 4 sides",
                "wooden plank floor, 2:1 isometric top-down view, seamlessly tileable on all 4 sides",
                "marble floor with patterns, 2:1 isometric perspective, seamlessly tileable on all 4 sides",
                "grassy meadow with wildflowers, 2:1 isometric top-down view, seamlessly tileable on all 4 sides",
                "rocky terrain, 2:1 isometric perspective, seamlessly tileable on all 4 sides",
                "swampy marsh, 2:1 isometric top-down view, seamlessly tileable on all 4 sides",
                "volcanic rock with cracks, 2:1 isometric perspective, seamlessly tileable on all 4 sides",
                "crystal cave floor, 2:1 isometric top-down view, seamlessly tileable on all 4 sides"
            });
            _examplePromptComboBox.SelectedIndex = 0;
            _examplePromptComboBox.SelectedIndexChanged += ExamplePromptComboBox_SelectedIndexChanged;
            
            // Generation style dropdown
            Label generationStyleLabel = new Label
            {
                Text = "Generation Style:",
                Location = new Point(0, currentY),
                Size = new Size(140, 22),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            currentY += 22;
            
            _generationStyleComboBox = new ComboBox
            {
                Location = new Point(0, currentY),
                Size = new Size(720, 25),
                Font = new Font("Segoe UI", 9),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat
            };
            currentY += 30 + sectionSpacing;
            
            // Add Pollinations AI generation styles
            _generationStyleComboBox.Items.AddRange(new object[]
            {
                "digital-art",
                "anime",
                "photographic",
                "cinematic",
                "3d-model",
                "pixel-art",
                "fantasy-art",
                "neon-punk",
                "isometric",
                "low-poly",
                "origami",
                "modeling-compound",
                "analog-film",
                "enhance",
                "cinematic-close-up"
            });
            _generationStyleComboBox.SelectedIndex = 0; // Default to digital-art
            
            // Action buttons section
            _generateButton = new Button
            {
                Text = "Generate",
                Location = new Point(420, currentY),
                Size = new Size(90, 35),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            _generateButton.FlatAppearance.BorderSize = 0;
            _generateButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 100, 195);
            _generateButton.Click += GenerateButton_Click;
            
            _retryButton = new Button
            {
                Text = "Retry",
                Location = new Point(520, currentY),
                Size = new Size(80, 35),
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(100, 100, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Visible = true,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _retryButton.FlatAppearance.BorderSize = 0;
            _retryButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(120, 120, 120);
            _retryButton.Click += RetryButton_Click;
            
            currentY += 45 + spacing;
            
            // Status section
            _statusLabel = new Label
            {
                Text = "Select an example or type your own prompt, then click Generate (Mouse wheel to zoom)",
                Location = new Point(0, currentY),
                Size = new Size(720, 20),
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(100, 100, 100),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            currentY += 24;
            
            _connectionStatusLabel = new Label
            {
                Text = "● Not connected",
                Location = new Point(0, currentY),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(150, 150, 150),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            currentY += 28;
            
            _progressBar = new ProgressBar
            {
                Location = new Point(0, currentY),
                Size = new Size(720, 25),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            currentY += 40 + sectionSpacing;
            
            // Tile Information section
            _tileInfoLabel = new Label
            {
                Text = "Tile Information:",
                Location = new Point(0, currentY),
                Size = new Size(200, 24),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            currentY += 28;
            
            _tileDimensionsLabel = new Label
            {
                Text = "Dimensions: 1024 × 512 pixels",
                Location = new Point(0, currentY),
                Size = new Size(720, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(70, 70, 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            currentY += 22;
            
            _tileAspectRatioLabel = new Label
            {
                Text = "Aspect Ratio: 2:1 (Isometric)",
                Location = new Point(0, currentY),
                Size = new Size(720, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(70, 70, 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            currentY += 22;
            
            _tileFormatLabel = new Label
            {
                Text = "Format: PNG with Alpha Channel (Transparency)",
                Location = new Point(0, currentY),
                Size = new Size(720, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(70, 70, 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            currentY += 22;
            
            _tileTemplateLabel = new Label
            {
                Text = "Template: Water.png (Diamond shape mask applied)",
                Location = new Point(0, currentY),
                Size = new Size(720, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(70, 70, 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            currentY += 28 + sectionSpacing;
            
            // Image preview section label
            Label previewLabel = new Label
            {
                Text = "Preview:",
                Location = new Point(0, currentY),
                Size = new Size(100, 24),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            currentY += 28;
            
            // Image preview panel with scroll support (512x256 for 2:1 aspect ratio)
            // Panel will be centered by CenterImagePreview() function
            _imagePanel = new Panel
            {
                Location = new Point(0, currentY),
                Size = new Size(512, 256),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black,
                AutoScroll = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            
            // Image preview PictureBox (inside panel)
            _imagePreview = new PictureBox
            {
                Location = new Point(0, 0),
                Size = new Size(512, 256),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Black
            };
            _imagePreview.MouseEnter += (s, e) => _imagePreview.Focus(); // Enable focus for mouse wheel
            _imagePreview.MouseWheel += ImagePreview_MouseWheel;
            _imagePanel.Controls.Add(_imagePreview);
            currentY += 270 + spacing;
            
            // Load button (below preview)
            _loadButton = new Button
            {
                Text = "Load Generated Image",
                Location = new Point(0, currentY),
                Size = new Size(200, 38),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 150, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _loadButton.FlatAppearance.BorderSize = 0;
            _loadButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 130, 0);
            _loadButton.Click += LoadButton_Click;
            currentY += 50 + spacing;
            
            // Close button
            Button closeButton = new Button
            {
                Text = "Close",
                Location = new Point(520, currentY),
                Size = new Size(80, 35),
                DialogResult = DialogResult.OK,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(220, 220, 220),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 200, 200);
            closeButton.Click += (s, e) => this.Close();
            
            this.Controls.Add(titleLabel);
            this.Controls.Add(promptLabel);
            this.Controls.Add(_promptTextBox);
            this.Controls.Add(exampleLabel);
            this.Controls.Add(_examplePromptComboBox);
            this.Controls.Add(generationStyleLabel);
            this.Controls.Add(_generationStyleComboBox);
            this.Controls.Add(_generateButton);
            this.Controls.Add(_retryButton);
            this.Controls.Add(_statusLabel);
            this.Controls.Add(_connectionStatusLabel);
            this.Controls.Add(_progressBar);
            this.Controls.Add(_tileInfoLabel);
            this.Controls.Add(_tileDimensionsLabel);
            this.Controls.Add(_tileAspectRatioLabel);
            this.Controls.Add(_tileFormatLabel);
            this.Controls.Add(_tileTemplateLabel);
            this.Controls.Add(previewLabel);
            this.Controls.Add(_imagePanel);
            this.Controls.Add(_loadButton);
            this.Controls.Add(closeButton);
            this.AcceptButton = _generateButton;
            this.CancelButton = closeButton;
            
            // Center the preview horizontally
            this.Resize += (s, e) => CenterImagePreview();
            CenterImagePreview();
            
            // Load and display the Water.png template as a guide
            LoadTemplateGuide();
        }
        
        private void LoadTemplateGuide()
        {
            // Load Water.png template to show as guide (use same path resolution as TileTextureLoader)
            string templatePath = "Content/sprites/tiles/template/Water.png";
            string? resolvedPath = ResolveTexturePath(templatePath);
            
            if (resolvedPath != null && File.Exists(resolvedPath))
            {
                try
                {
                    using (FileStream fs = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read))
                    {
                        Image templateImage = Image.FromStream(fs);
                        
                        // Dispose old image
                        if (_originalImage != null)
                        {
                            _originalImage.Dispose();
                        }
                        
                        // Store as original
                        _originalImage = new Bitmap(templateImage);
                        templateImage.Dispose();
                        
                        // Reset zoom and display
                        _zoomLevel = 1.0f;
                        UpdateImageZoom();
                        
                        _statusLabel.Text = "Template guide loaded (this shows the diamond shape). Enter a prompt and click Generate.";
                        _statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
                    }
                }
                catch (Exception ex)
                {
                    _statusLabel.Text = $"Could not load template guide: {ex.Message}";
                    _statusLabel.ForeColor = Color.FromArgb(200, 0, 0);
                }
            }
            else
            {
                _statusLabel.Text = "Template guide not found. Enter a prompt and click Generate.";
                _statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
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
            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
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

            return null;
        }
        
        private Bitmap ApplyTemplateMask(Image generatedImage)
        {
            // Load Water.png template (use same path resolution as TileTextureLoader)
            string templatePath = "Content/sprites/tiles/template/Water.png";
            string? resolvedPath = ResolveTexturePath(templatePath);
            
            // Resize generated image to 1024x512
            Bitmap resizedGenerated = new Bitmap(1024, 512);
            using (Graphics g = Graphics.FromImage(resizedGenerated))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(generatedImage, 0, 0, 1024, 512);
            }
            
            // If template doesn't exist, return resized image
            if (resolvedPath == null || !File.Exists(resolvedPath))
            {
                return resizedGenerated;
            }
            
            // Load template image
            Bitmap template;
            using (FileStream fs = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read))
            {
                template = new Bitmap(Image.FromStream(fs));
            }
            
            // Ensure template is also 1024x512
            Bitmap resizedTemplate = new Bitmap(1024, 512);
            using (Graphics g = Graphics.FromImage(resizedTemplate))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(template, 0, 0, 1024, 512);
            }
            
            // Create result image (1024x512)
            Bitmap maskedResult = new Bitmap(1024, 512, PixelFormat.Format32bppArgb);
            
            // Apply template's alpha channel as mask to generated image using LockBits
            BitmapData resultData = maskedResult.LockBits(
                new Rectangle(0, 0, 1024, 512),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb
            );
            BitmapData generatedData = resizedGenerated.LockBits(
                new Rectangle(0, 0, 1024, 512),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb
            );
            BitmapData templateData = resizedTemplate.LockBits(
                new Rectangle(0, 0, 1024, 512),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb
            );
            
            try
            {
                // Process pixels row by row
                for (int y = 0; y < 512; y++)
                {
                    IntPtr resultRow = resultData.Scan0 + (y * resultData.Stride);
                    IntPtr generatedRow = generatedData.Scan0 + (y * generatedData.Stride);
                    IntPtr templateRow = templateData.Scan0 + (y * templateData.Stride);
                    
                    byte[] resultRowBytes = new byte[resultData.Stride];
                    byte[] generatedRowBytes = new byte[generatedData.Stride];
                    byte[] templateRowBytes = new byte[templateData.Stride];
                    
                    Marshal.Copy(generatedRow, generatedRowBytes, 0, generatedData.Stride);
                    Marshal.Copy(templateRow, templateRowBytes, 0, templateData.Stride);
                    
                    for (int x = 0; x < 1024; x++)
                    {
                        int offset = x * 4;
                        
                        // Copy RGB from generated image
                        resultRowBytes[offset + 0] = generatedRowBytes[offset + 0]; // B
                        resultRowBytes[offset + 1] = generatedRowBytes[offset + 1]; // G
                        resultRowBytes[offset + 2] = generatedRowBytes[offset + 2]; // R
                        
                        // Apply template's alpha channel
                        resultRowBytes[offset + 3] = templateRowBytes[offset + 3]; // A
                    }
                    
                    Marshal.Copy(resultRowBytes, 0, resultRow, resultData.Stride);
                }
            }
            finally
            {
                maskedResult.UnlockBits(resultData);
                resizedGenerated.UnlockBits(generatedData);
                resizedTemplate.UnlockBits(templateData);
            }
            
            // Clean up
            resizedGenerated.Dispose();
            resizedTemplate.Dispose();
            template.Dispose();
            
            return maskedResult;
        }
        
        private void ExamplePromptComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_examplePromptComboBox != null && _examplePromptComboBox.SelectedIndex > 0)
            {
                string selectedPrompt = _examplePromptComboBox.SelectedItem?.ToString() ?? "";
                if (!string.IsNullOrEmpty(selectedPrompt))
                {
                    _promptTextBox.Text = selectedPrompt;
                }
            }
        }
        
        private void CenterImagePreview()
        {
            if (_imagePanel != null)
            {
                int clientWidth = this.ClientSize.Width;
                _imagePanel.Left = (clientWidth - _imagePanel.Width) / 2;
            }
        }
        
        private void ImagePreview_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (_originalImage == null)
                return;
            
            // Determine zoom direction
            float zoomDelta = e.Delta > 0 ? ZOOM_STEP : -ZOOM_STEP;
            float newZoom = Math.Max(ZOOM_MIN, Math.Min(ZOOM_MAX, _zoomLevel + zoomDelta));
            
            if (newZoom != _zoomLevel)
            {
                _zoomLevel = newZoom;
                UpdateImageZoom();
            }
        }
        
        private void UpdateImageZoom()
        {
            if (_originalImage == null)
                return;
            
            // Calculate new size based on zoom, maintaining the original image's aspect ratio
            // Base display size maintains 2:1 aspect ratio (1024x512 -> 512x256 when zoom = 1.0)
            float aspectRatio = (float)_originalImage.Width / _originalImage.Height;
            int baseDisplayWidth = 512;
            int baseDisplayHeight = (int)(baseDisplayWidth / aspectRatio);
            int newWidth = (int)(baseDisplayWidth * _zoomLevel);
            int newHeight = (int)(baseDisplayHeight * _zoomLevel);
            
            // Create scaled image
            Bitmap scaledImage = new Bitmap(_originalImage, newWidth, newHeight);
            
            // Dispose old displayed image
            if (_imagePreview.Image != null && _imagePreview.Image != _originalImage)
            {
                _imagePreview.Image.Dispose();
            }
            
            // Update PictureBox size and image
            _imagePreview.Size = new Size(newWidth, newHeight);
            _imagePreview.Image = scaledImage;
            
            _statusLabel.Text = $"Zoom: {(_zoomLevel * 100):F0}% - Use mouse wheel to zoom";
        }
        
        private async void GenerateButton_Click(object? sender, EventArgs e)
        {
            string prompt = _promptTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(prompt))
            {
                MessageBox.Show("Please enter a prompt.", "No Prompt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            _generateButton.Enabled = false;
            _retryButton.Enabled = false; // Disable retry button during generation
            _loadButton.Enabled = false; // Disable load button during generation
            
            // Dispose of previous images
            if (_imagePreview.Image != null && _imagePreview.Image != _originalImage)
            {
                _imagePreview.Image.Dispose();
                _imagePreview.Image = null;
            }
            if (_originalImage != null)
            {
                _originalImage.Dispose();
                _originalImage = null;
            }
            _zoomLevel = 1.0f;
            
            _progressBar.Visible = true;
            _progressBar.Style = ProgressBarStyle.Marquee;
            UpdateConnectionStatus(false, "Connecting...");
            _statusLabel.Text = "Connecting to Pollinations AI...";
            _statusLabel.ForeColor = Color.FromArgb(0, 120, 215);
            Application.DoEvents();
            
            try
            {
                // Build the URL with the prompt (add timestamp to prevent caching)
                // Always add 2:1 isometric perspective and seamless on all 4 sides requirements
                // The mask will enforce the diamond shape and transparency
                // Use 1024x512 dimensions to match isometric tile format (2:1 ratio)
                string enhancedPrompt;
                string lowerPrompt = prompt.ToLower();
                if ((lowerPrompt.Contains("2:1") || lowerPrompt.Contains("isometric")) && lowerPrompt.Contains("seamless"))
                {
                    // Prompt already includes some requirements, ensure it mentions all 4 sides
                    if (!lowerPrompt.Contains("all 4 sides") && !lowerPrompt.Contains("four sides"))
                    {
                        enhancedPrompt = $"{prompt}, seamlessly tileable on all 4 sides, tile texture, 1024x512 pixels";
                    }
                    else
                    {
                        enhancedPrompt = $"{prompt}, tile texture, 1024x512 pixels";
                    }
                }
                else
                {
                    // Add 2:1 isometric and seamless on all 4 sides requirements
                    enhancedPrompt = $"2:1 isometric top-down view, {prompt}, seamlessly tileable on all 4 sides, tile texture, 1024x512 pixels";
                }
                // Get selected generation style
                string generationStyle = _generationStyleComboBox.SelectedItem?.ToString() ?? "digital-art";
                
                // Add generation style to the prompt
                string styleEnhancedPrompt = $"{enhancedPrompt}, {generationStyle} style";
                
                string encodedPrompt = Uri.EscapeDataString(styleEnhancedPrompt);
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                // Build URL with generation style as model parameter
                // Pollinations API uses 'model' parameter to specify the generation style
                string url = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width=1024&height=512&nologo=true&enhance=true&model={generationStyle}&_t={timestamp}";
                
                UpdateConnectionStatus(true, "Connected");
                _statusLabel.Text = "Requesting image generation...";
                Application.DoEvents();
                
                // Download the image with progress updates
                _statusLabel.Text = "Generating image (this may take 10-30 seconds)...";
                Application.DoEvents();
                
                using (HttpResponseMessage response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        _statusLabel.Text = "Downloading image...";
                        Application.DoEvents();
                        
                        byte[] imageData = await response.Content.ReadAsByteArrayAsync();
                        
                        _statusLabel.Text = "Processing image...";
                        Application.DoEvents();
                        
                        // Load the image
                        using (MemoryStream ms = new MemoryStream(imageData))
                        {
                            Image loadedImage = Image.FromStream(ms);
                            
                            // Apply Water.png template mask to enforce exact diamond shape
                            _statusLabel.Text = "Applying template mask...";
                            Application.DoEvents();
                            
                            Bitmap maskedImage = ApplyTemplateMask(loadedImage);
                            
                            // Dispose loaded image
                            loadedImage.Dispose();
                            
                            // Dispose of old original image
                            if (_originalImage != null)
                            {
                                _originalImage.Dispose();
                            }
                            
                            // Store original image (masked)
                            _originalImage = maskedImage;
                            
                            // Dispose of old displayed image
                            if (_imagePreview.Image != null)
                            {
                                _imagePreview.Image.Dispose();
                            }
                            
                            // Reset zoom to 1.0
                            _zoomLevel = 1.0f;
                            
                            // Display image at current zoom level
                            UpdateImageZoom();
                            
                            _statusLabel.Text = "Image generated successfully! (Mouse wheel to zoom)";
                            _statusLabel.ForeColor = Color.FromArgb(0, 150, 0);
                            UpdateConnectionStatus(true, "Connected - Success");
                            
                            // Enable Load button
                            _loadButton.Enabled = true;
                        }
                    }
                    else
                    {
                        throw new HttpRequestException($"Server returned status code: {response.StatusCode}");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _statusLabel.Text = "Request timed out. Please try again.";
                _statusLabel.ForeColor = Color.FromArgb(200, 0, 0);
                UpdateConnectionStatus(false, "Connection timeout");
                MessageBox.Show("The request took too long and timed out. Please try again with a different prompt.", "Timeout Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (HttpRequestException ex)
            {
                _statusLabel.Text = $"Connection error: {ex.Message}";
                _statusLabel.ForeColor = Color.FromArgb(200, 0, 0);
                UpdateConnectionStatus(false, "Connection failed");
                MessageBox.Show($"Error connecting to Pollinations AI: {ex.Message}\n\nPlease check your internet connection and try again.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
                _statusLabel.ForeColor = Color.FromArgb(200, 0, 0);
                UpdateConnectionStatus(false, "Error occurred");
                MessageBox.Show($"Error generating image: {ex.Message}", "Generation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _generateButton.Enabled = true;
                _progressBar.Visible = false;
                // Show and enable retry button after generation attempt
                _retryButton.Visible = true;
                _retryButton.Enabled = true;
            }
        }
        
        private void RetryButton_Click(object? sender, EventArgs e)
        {
            // Reconnect and restart the generation process
            // Reset connection status
            UpdateConnectionStatus(false, "Reconnecting...");
            _statusLabel.Text = "Reconnecting and restarting generation...";
            _statusLabel.ForeColor = Color.FromArgb(0, 120, 215);
            Application.DoEvents();
            
            // Dispose and recreate HttpClient to ensure fresh connection
            try
            {
                _httpClient?.Dispose();
            }
            catch { }
            
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            
            // Call the generate button click handler to restart the process
            GenerateButton_Click(sender, e);
        }
        
        private void LoadButton_Click(object? sender, EventArgs e)
        {
            if (_originalImage == null)
            {
                MessageBox.Show("No image to save. Please generate an image first.", "No Image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "PNG files (*.png)|*.png|All files (*.*)|*.*";
                dialog.Title = "Save Generated Tile Image";
                dialog.FileName = "generated_tile.png";
                dialog.DefaultExt = "png";
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        _originalImage.Save(dialog.FileName, ImageFormat.Png);
                        MessageBox.Show($"Image saved successfully to:\n{dialog.FileName}", "Save Image", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        
        private void UpdateConnectionStatus(bool connected, string statusText)
        {
            if (_connectionStatusLabel != null)
            {
                _connectionStatusLabel.Text = connected 
                    ? $"● {statusText}" 
                    : $"○ {statusText}";
                _connectionStatusLabel.ForeColor = connected 
                    ? Color.FromArgb(0, 150, 0) 
                    : Color.FromArgb(200, 0, 0);
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
                if (_imagePreview?.Image != null && _imagePreview.Image != _originalImage)
                {
                    _imagePreview.Image.Dispose();
                }
                _originalImage?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
