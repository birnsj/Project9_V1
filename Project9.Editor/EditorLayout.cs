using System;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace Project9.Editor
{
    public class EditorLayout
    {
        private static readonly string LayoutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Project9",
            "editor_layout.json"
        );

        public WindowLayout MainWindow { get; set; } = new WindowLayout();
        public WindowLayout? EnemyPropertiesWindow { get; set; }
        public WindowLayout? PlayerPropertiesWindow { get; set; }
        public WindowLayout? CameraPropertiesWindow { get; set; }
        public WindowLayout? WeaponPropertiesWindow { get; set; }
        public WindowLayout? WorldObjectPropertiesWindow { get; set; }
        public WindowLayout? CollisionWindow { get; set; }
        public WindowLayout? TileBrowserWindow { get; set; }
        public ViewSettings? View { get; set; }

        public static EditorLayout Load()
        {
            try
            {
                if (File.Exists(LayoutPath))
                {
                    string json = File.ReadAllText(LayoutPath);
                    var layout = JsonSerializer.Deserialize<EditorLayout>(json);
                    return layout ?? new EditorLayout();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading editor layout: {ex.Message}");
            }

            return new EditorLayout();
        }

        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(LayoutPath) ?? "";
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(LayoutPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving editor layout: {ex.Message}");
            }
        }

        public class WindowLayout
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool Visible { get; set; }
            public bool IsDocked { get; set; }
            public int WindowState { get; set; } // 0=Normal, 1=Minimized, 2=Maximized

            public static WindowLayout FromForm(Form form, bool isDocked = false)
            {
                return new WindowLayout
                {
                    X = form.Left,
                    Y = form.Top,
                    Width = form.Width,
                    Height = form.Height,
                    Visible = form.Visible,
                    IsDocked = isDocked,
                    WindowState = (int)form.WindowState
                };
            }

            public void ApplyToForm(Form form, bool isDocked)
            {
                if (!isDocked || !this.IsDocked)
                {
                    // Only restore position/size if window is not docked
                    if (this.Width > 0 && this.Height > 0)
                    {
                        form.Location = new Point(this.X, this.Y);
                        form.Size = new Size(this.Width, this.Height);
                    }
                    
                    if (this.WindowState >= 0 && this.WindowState <= 2)
                    {
                        form.WindowState = (FormWindowState)this.WindowState;
                    }
                }

                form.Visible = this.Visible;
            }
        }

        public class ViewSettings
        {
            public bool ShowGrid32x16 { get; set; }
            public bool ShowGrid64x32 { get; set; }
            public bool ShowGrid128x64 { get; set; }
            public bool ShowGrid512x256 { get; set; }
            public bool ShowGrid1024x512 { get; set; }
            public float TileOpacity { get; set; } = 0.7f;
            public float BoundingBoxOpacity { get; set; } = 0.3f;
            public bool ShowEnemyCones { get; set; } = true;
            public bool ShowCameraCones { get; set; } = true;
            public float CameraPositionX { get; set; }
            public float CameraPositionY { get; set; }
            public float CameraZoom { get; set; } = 1.0f;
        }
    }
}
