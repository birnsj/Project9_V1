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

        public void FollowTarget(Vector2 targetPosition, Vector2 screenCenter)
        {
            _position = targetPosition - screenCenter / _zoom;
        }

        public void Pan(Vector2 direction, float deltaTime)
        {
            float panSpeed = 300.0f;
            _position += direction * panSpeed * deltaTime;
        }
    }
}

