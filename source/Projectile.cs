using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    /// <summary>
    /// Represents a projectile (bullet) fired from a gun
    /// </summary>
    public class Projectile : Entity
    {
        private Vector2 _direction;
        private float _speed;
        private float _damage;
        private float _lifetime;
        private float _maxLifetime;
        private bool _isPlayerProjectile;
        private static Texture2D? _projectileTexture;

        public float Damage => _damage;
        public bool IsPlayerProjectile => _isPlayerProjectile;
        public bool IsExpired => _lifetime >= _maxLifetime;

        /// <summary>
        /// Create a new projectile
        /// </summary>
        /// <param name="position">Starting position</param>
        /// <param name="direction">Direction vector (will be normalized)</param>
        /// <param name="speed">Speed in pixels per second</param>
        /// <param name="damage">Damage dealt on hit</param>
        /// <param name="isPlayerProjectile">True if fired by player, false if by enemy</param>
        /// <param name="lifetime">Maximum lifetime in seconds</param>
        public Projectile(Vector2 position, Vector2 direction, float speed, float damage, bool isPlayerProjectile, float lifetime = 3.0f)
            : base(position, Color.Yellow, 0f, 0f, 1f) // No movement speed, minimal health
        {
            direction.Normalize();
            _direction = direction;
            _speed = speed;
            _damage = damage;
            _isPlayerProjectile = isPlayerProjectile;
            _maxLifetime = lifetime;
            _lifetime = 0.0f;
            _size = 4; // Smaller projectile size
        }

        public override void Update(float deltaTime)
        {
            // Update lifetime
            _lifetime += deltaTime;
            
            if (IsExpired)
                return;

            // Move projectile
            Vector2 movement = _direction * _speed * deltaTime;
            _position += movement;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (IsExpired)
                return;

            // Create texture if needed
            if (_projectileTexture == null)
            {
                _projectileTexture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
                _projectileTexture.SetData(new[] { Color.White });
            }

            if (_projectileTexture != null)
            {
                // Draw projectile as a bright circle/point
                Color projectileColor = _isPlayerProjectile ? new Color(255, 255, 0) : Color.Orange; // Bright yellow
                
                // Draw smaller projectile
                float drawSize = _size * 1.5f; // Make it 6 pixels for player projectiles (smaller)
                
                spriteBatch.Draw(
                    _projectileTexture,
                    _position,
                    null,
                    projectileColor,
                    0.0f,
                    new Vector2(0.5f, 0.5f), // Center the projectile
                    new Vector2(drawSize, drawSize),
                    SpriteEffects.None,
                    0.0f
                );
            }
        }
    }
}

