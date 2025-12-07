using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.Brushes;

namespace Project9
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        private Camera _camera = null!;
        private IsometricMap _map = null!;
        private Player _player = null!;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private Desktop _desktop = null!;
        private Label _positionLabel = null!;
        private Label _zoomLabel = null!;
        private Button _resetButton = null!;
        private Vector2 _initialCameraPosition;
        private SpriteFont? _uiFont;
        private Vector2 _screenCenter;
        private bool _cameraFollowingPlayer = true;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _camera = new Camera();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _map = new IsometricMap(Content, GraphicsDevice);

            // Initialize Myra
            MyraEnvironment.Game = this;

            // Load font for Myra UI - Myra needs a SpriteFont to render text
            try
            {
                _uiFont = Content.Load<SpriteFont>("Arial");
            }
            catch (Exception ex)
            {
                // If font loading fails, Myra will try to use default
                System.Diagnostics.Debug.WriteLine($"Failed to load font: {ex.Message}");
                _uiFont = null;
            }

            // Calculate screen center
            _screenCenter = new Vector2(
                GraphicsDevice.Viewport.Width / 2.0f,
                GraphicsDevice.Viewport.Height / 2.0f
            );

            // Initialize player at map center
            Vector2 mapCenter = _map.GetMapCenter();
            _player = new Player(mapCenter);

            // Center camera on player initially (set directly, no lerp on first frame)
            Vector2 desiredCameraPos = _player.Position - _screenCenter / _camera.Zoom;
            _camera.Position = desiredCameraPos;
            _initialCameraPosition = _camera.Position;

            CreateUI();
        }

        private void CreateUI()
        {
            _desktop = new Desktop();

            // Panel background - smaller vertically
            var panel = new Panel
            {
                Width = 250,
                Height = 80,
                Background = new SolidBrush(new Microsoft.Xna.Framework.Color(40, 40, 40, 230))
            };

            // Position label
            _positionLabel = new Label
            {
                Text = "Position: (0, 0)",
                TextColor = Microsoft.Xna.Framework.Color.White,
                Left = 10,
                Top = 10,
                Width = 230,
                Padding = new Myra.Graphics2D.Thickness(5)
            };
            panel.Widgets.Add(_positionLabel);

            // Zoom label
            _zoomLabel = new Label
            {
                Text = "Zoom: 1.0x",
                TextColor = Microsoft.Xna.Framework.Color.White,
                Left = 10,
                Top = 35,
                Width = 230,
                Padding = new Myra.Graphics2D.Thickness(5)
            };
            panel.Widgets.Add(_zoomLabel);

            // Create container for panel and button
            var container = new VerticalStackPanel
            {
                Left = 10,
                Top = 10,
                Spacing = 5
            };

            // Add panel to container
            container.Widgets.Add(panel);

            // Reset button - outside the panel, below it
            _resetButton = new Button
            {
                Content = new Label 
                { 
                    Text = "Reset Position",
                    TextColor = Microsoft.Xna.Framework.Color.White
                },
                Width = 150,
                Height = 35,
                Padding = new Myra.Graphics2D.Thickness(5)
            };
            _resetButton.Click += (s, a) =>
            {
                // Reset player to map center
                Vector2 mapCenter = _map.GetMapCenter();
                _player.Position = mapCenter;
                _player.ClearTarget();
                _camera.FollowTarget(_player.Position, _screenCenter);
                _camera.Zoom = 1.0f;
            };
            container.Widgets.Add(_resetButton);

            _desktop.Root = container;
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || 
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            KeyboardState currentKeyboardState = Keyboard.GetState();
            MouseState currentMouseState = Mouse.GetState();
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Handle left Ctrl for sneak mode toggle
            if (currentKeyboardState.IsKeyDown(Keys.LeftControl) && 
                !_previousKeyboardState.IsKeyDown(Keys.LeftControl))
            {
                _player.ToggleSneak();
            }

            // Handle space bar to return camera to following player
            if (currentKeyboardState.IsKeyDown(Keys.Space) && 
                !_previousKeyboardState.IsKeyDown(Keys.Space))
            {
                _cameraFollowingPlayer = true;
            }

            // Handle WASD for camera panning
            Vector2 panDirection = Vector2.Zero;
            if (currentKeyboardState.IsKeyDown(Keys.W))
                panDirection.Y -= 1;
            if (currentKeyboardState.IsKeyDown(Keys.S))
                panDirection.Y += 1;
            if (currentKeyboardState.IsKeyDown(Keys.A))
                panDirection.X -= 1;
            if (currentKeyboardState.IsKeyDown(Keys.D))
                panDirection.X += 1;

            // If WASD is pressed, switch to manual camera mode
            if (panDirection != Vector2.Zero)
            {
                _cameraFollowingPlayer = false;
                panDirection.Normalize();
                _camera.Pan(panDirection, deltaTime);
            }

            // Handle mouse wheel for zoom
            int scrollDelta = currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                float zoomAmount = scrollDelta > 0 ? 0.1f : -0.1f;
                if (zoomAmount > 0)
                    _camera.ZoomIn(zoomAmount);
                else
                    _camera.ZoomOut(Math.Abs(zoomAmount));
            }

            // Handle mouse input for player movement
            Vector2 mouseScreenPos = new Vector2(currentMouseState.X, currentMouseState.Y);
            Vector2 mouseWorldPos = ScreenToWorld(mouseScreenPos);

            // Left click: set target position
            if (currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                _player.SetTarget(mouseWorldPos);
            }

            // Left button held: follow mouse cursor
            Vector2? followPosition = null;
            if (currentMouseState.LeftButton == ButtonState.Pressed)
            {
                followPosition = mouseWorldPos;
            }
            else if (currentMouseState.LeftButton == ButtonState.Released && 
                     _previousMouseState.LeftButton == ButtonState.Pressed)
            {
                // Button released - clear follow but keep target if set
            }

            // Update player movement
            _player.Update(followPosition, deltaTime);

            // Update camera - follow player if in follow mode, otherwise manual control
            if (_cameraFollowingPlayer)
            {
                _camera.FollowTarget(_player.Position, _screenCenter);
            }

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;

            // Update UI information
            if (_positionLabel != null)
            {
                _positionLabel.Text = $"Position: ({_camera.Position.X:F1}, {_camera.Position.Y:F1})";
            }
            if (_zoomLabel != null)
            {
                _zoomLabel.Text = $"Zoom: {_camera.Zoom:F2}x";
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateGray);

            _spriteBatch.Begin(
                transformMatrix: _camera.GetTransform(),
                samplerState: SamplerState.PointClamp
            );

            _map.Draw(_spriteBatch);

            // Draw player
            _player.Draw(_spriteBatch);

            _spriteBatch.End();

            // Draw version number in lower right corner
            if (_uiFont != null)
            {
                string versionText = "V001";
                Vector2 textSize = _uiFont.MeasureString(versionText);
                Vector2 position = new Vector2(
                    GraphicsDevice.Viewport.Width - textSize.X - 10,
                    GraphicsDevice.Viewport.Height - textSize.Y - 10
                );

                _spriteBatch.Begin();
                _spriteBatch.DrawString(_uiFont, versionText, position, Color.White);
                _spriteBatch.End();
            }

            // Draw UI
            _desktop.Render();

            base.Draw(gameTime);
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            // Convert screen coordinates to world coordinates
            // Camera transform: Translate(-position) * Scale(zoom)
            // Inverse transform: Scale(1/zoom) * Translate(position)
            // So: world = screen / zoom + position
            Vector2 worldPos = screenPosition / _camera.Zoom + _camera.Position;
            return worldPos;
        }
    }
}
