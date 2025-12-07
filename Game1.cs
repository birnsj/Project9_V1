using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Project9
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        private Camera _camera = null!;
        private IsometricMap _map = null!;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;

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
            _map = new IsometricMap(GraphicsDevice);

            // Center camera on map initially (offset by screen center to properly center the view)
            Vector2 mapCenter = _map.GetMapCenter();
            Vector2 screenCenter = new Vector2(
                GraphicsDevice.Viewport.Width / 2.0f,
                GraphicsDevice.Viewport.Height / 2.0f
            );
            // Initial zoom is 1.0, so divide by 1.0 (no change needed)
            _camera.Position = mapCenter - screenCenter;
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || 
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            KeyboardState currentKeyboardState = Keyboard.GetState();
            MouseState currentMouseState = Mouse.GetState();

            // Handle WASD input for panning
            Vector2 panDirection = Vector2.Zero;
            
            if (currentKeyboardState.IsKeyDown(Keys.W))
                panDirection.Y -= 1;
            if (currentKeyboardState.IsKeyDown(Keys.S))
                panDirection.Y += 1;
            if (currentKeyboardState.IsKeyDown(Keys.A))
                panDirection.X -= 1;
            if (currentKeyboardState.IsKeyDown(Keys.D))
                panDirection.X += 1;

            if (panDirection != Vector2.Zero)
            {
                panDirection.Normalize();
                _camera.Pan(panDirection, (float)gameTime.ElapsedGameTime.TotalSeconds);
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

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;

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

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
