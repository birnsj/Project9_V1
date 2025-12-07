using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    public class Player
    {
        private Vector2 _position;
        private Vector2? _targetPosition;
        private float _walkSpeed;
        private float _runSpeed;
        private float _sneakSpeed;
        private float _distanceThreshold;
        private float _currentSpeed;
        private Texture2D? _texture;
        private Color _color;
        private Color _normalColor;
        private Color _sneakColor;
        private int _size;
        private bool _isSneaking;

        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        public float WalkSpeed
        {
            get => _walkSpeed;
            set => _walkSpeed = value;
        }

        public float RunSpeed
        {
            get => _runSpeed;
            set => _runSpeed = value;
        }

        public float CurrentSpeed => _currentSpeed;

        public bool IsSneaking => _isSneaking;

        public void ToggleSneak()
        {
            _isSneaking = !_isSneaking;
            _color = _isSneaking ? _sneakColor : _normalColor;
        }

        public Player(Vector2 startPosition)
        {
            _position = startPosition;
            _targetPosition = null;
            _walkSpeed = 75.0f; // pixels per second
            _runSpeed = 150.0f; // pixels per second
            _sneakSpeed = _walkSpeed / 2.0f; // half of walk speed
            _distanceThreshold = 100.0f; // pixels
            _currentSpeed = 0.0f;
            _normalColor = Color.Red;
            _sneakColor = Color.Purple;
            _color = _normalColor;
            _size = 32;
            _isSneaking = false;
        }

        public void SetTarget(Vector2 target)
        {
            _targetPosition = target;
        }

        public void ClearTarget()
        {
            _targetPosition = null;
            _currentSpeed = 0.0f;
        }

        public void Update(Vector2? followPosition, float deltaTime)
        {
            Vector2? moveTarget = null;

            // Priority: follow position (mouse held) > target position (click)
            if (followPosition.HasValue)
            {
                // Add dead zone to prevent jitter when mouse is very close
                // Larger dead zone when sneaking due to slower movement
                float deadZone = _isSneaking ? 8.0f : 2.0f;
                Vector2 direction = followPosition.Value - _position;
                float distance = direction.Length();
                
                // Only update target if mouse moved significantly (dead zone)
                if (distance > deadZone)
                {
                    moveTarget = followPosition.Value;
                }
                else
                {
                    // Mouse is very close, keep current target or stop
                    if (_targetPosition.HasValue)
                    {
                        moveTarget = _targetPosition.Value;
                    }
                }
            }
            else if (_targetPosition.HasValue)
            {
                moveTarget = _targetPosition.Value;
            }

            if (moveTarget.HasValue)
            {
                Vector2 direction = moveTarget.Value - _position;
                float distance = direction.Length();

                // Determine speed based on sneak mode or distance
                if (_isSneaking)
                {
                    // Sneak mode: always use sneak speed, not affected by distance
                    _currentSpeed = _sneakSpeed;
                }
                else
                {
                    // Normal mode: use walk/run based on distance
                    if (distance < _distanceThreshold)
                    {
                        _currentSpeed = _walkSpeed;
                    }
                    else
                    {
                        _currentSpeed = _runSpeed;
                    }
                }

                // Move towards target
                // Larger stop threshold when sneaking to prevent jitter
                float stopThreshold = _isSneaking ? 10.0f : 5.0f;
                if (distance > stopThreshold)
                {
                    direction.Normalize();
                    float moveDistance = _currentSpeed * deltaTime;
                    
                    // Don't overshoot the target
                    if (moveDistance > distance)
                    {
                        moveDistance = distance;
                    }

                    _position += direction * moveDistance;
                }
                else
                {
                    // Reached target - use smoother approach, especially when sneaking
                    if (distance > 1.0f)
                    {
                        // Smooth approach - more gradual when sneaking
                        float approachFactor = _isSneaking ? 0.3f : 0.5f;
                        direction.Normalize();
                        _position += direction * distance * approachFactor;
                    }
                    else
                    {
                        // Very close, snap to target
                        _position = moveTarget.Value;
                    }
                    
                    if (!followPosition.HasValue && distance < stopThreshold)
                    {
                        // Only clear target if not following mouse and close enough
                        _targetPosition = null;
                        _currentSpeed = 0.0f;
                    }
                }
            }
            else
            {
                _currentSpeed = 0.0f;
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_texture == null)
            {
                // Create a simple colored rectangle texture if not loaded
                _texture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
                _texture.SetData(new[] { _color });
            }

            // Draw player centered at position
            Vector2 drawPosition = _position - new Vector2(_size / 2.0f, _size / 2.0f);
            spriteBatch.Draw(_texture, new Rectangle((int)drawPosition.X, (int)drawPosition.Y, _size, _size), _color);
        }
    }
}

