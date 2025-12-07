using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Project9.Editor
{
    public class EditorCamera
    {
        private PointF _position;
        private float _zoom;
        private readonly float _minZoom;
        private readonly float _maxZoom;
        private readonly float _panSpeed;

        public PointF Position
        {
            get => _position;
            set => _position = value;
        }

        public float Zoom
        {
            get => _zoom;
            set => _zoom = Math.Clamp(value, _minZoom, _maxZoom);
        }

        public EditorCamera()
        {
            _position = PointF.Empty;
            _zoom = 1.0f;
            _minZoom = 0.5f;
            _maxZoom = 4.0f;
            _panSpeed = 300.0f;
        }

        public Matrix GetTransformMatrix()
        {
            var matrix = new Matrix();
            matrix.Translate(-_position.X, -_position.Y);
            matrix.Scale(_zoom, _zoom);
            return matrix;
        }

        public void Pan(PointF direction, float deltaTime)
        {
            if (direction.X != 0 || direction.Y != 0)
            {
                float length = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
                if (length > 0)
                {
                    direction = new PointF(direction.X / length, direction.Y / length);
                    _position = new PointF(
                        _position.X + direction.X * _panSpeed * deltaTime,
                        _position.Y + direction.Y * _panSpeed * deltaTime
                    );
                }
            }
        }

        public void ZoomIn(float amount)
        {
            Zoom += amount;
        }

        public void ZoomOut(float amount)
        {
            Zoom -= amount;
        }
    }
}


