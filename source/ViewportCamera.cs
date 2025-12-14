using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    /// <summary>
    /// Camera for viewport/view control (zooming, panning, following targets)
    /// </summary>
    public class ViewportCamera
    {
        private Vector2 _position;
        private float _zoom;
        private const float FOLLOW_SPEED = 8.0f; // Smooth follow speed (higher = faster)

        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        public float Zoom
        {
            get => _zoom;
            set => _zoom = MathHelper.Clamp(value, 0.5f, 4.0f);
        }

        public ViewportCamera()
        {
            _position = Vector2.Zero;
            _zoom = 1.0f;
        }

        public Matrix GetTransform()
        {
            return Matrix.CreateTranslation(-_position.X, -_position.Y, 0) *
                   Matrix.CreateScale(_zoom);
        }

        /// <summary>
        /// Instantly snap camera to target (for button clicks, etc.)
        /// </summary>
        public void FollowTarget(Vector2 targetPosition, Vector2 screenCenter)
        {
            _position = targetPosition - screenCenter / _zoom;
        }
        
        /// <summary>
        /// Smoothly follow target with frame-rate independent interpolation
        /// </summary>
        public void FollowTarget(Vector2 targetPosition, Vector2 screenCenter, float deltaTime)
        {
            // Calculate target camera position
            Vector2 targetCameraPos = targetPosition - screenCenter / _zoom;
            
            // Smoothly interpolate towards target position (frame-rate independent)
            float lerpAmount = MathHelper.Clamp(FOLLOW_SPEED * deltaTime, 0.0f, 1.0f);
            _position = Vector2.Lerp(_position, targetCameraPos, lerpAmount);
        }

        public void Pan(Vector2 direction, float deltaTime)
        {
            float panSpeed = 300.0f;
            _position += direction * panSpeed * deltaTime;
        }
    }
}

