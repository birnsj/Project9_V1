using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    /// <summary>
    /// Represents a weapon that can be picked up from the ground
    /// </summary>
    public class WeaponPickup : Entity
    {
        private Weapon _weapon;
        private bool _isPickedUp = false;
        private static Texture2D? _lineTexture;

        public Weapon Weapon => _weapon;
        public bool IsPickedUp => _isPickedUp;

        public WeaponPickup(Vector2 position, Weapon weapon) 
            : base(position, weapon.WeaponColor, 0f, 0f, 1f) // No movement, minimal health
        {
            _weapon = weapon;
            _size = 24; // Smaller than entities
        }

        /// <summary>
        /// Mark this weapon as picked up
        /// </summary>
        public void PickUp()
        {
            _isPickedUp = true;
        }

        /// <summary>
        /// Update weapon pickup (no-op since weapons don't move)
        /// </summary>
        public override void Update(float deltaTime)
        {
            // Weapons don't need to update
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (_isPickedUp)
                return; // Don't draw if picked up

            // Create texture if needed
            if (_lineTexture == null)
            {
                _lineTexture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
                _lineTexture.SetData(new[] { Color.White });
            }

            if (_lineTexture != null)
            {
                // Draw gun as a yellow circle, sword as a line
                if (_weapon is Gun)
                {
                    // Draw gun as a yellow circle
                    float circleRadius = 12.0f; // Radius of the circle
                    Color circleColor = Color.Yellow;
                    
                    // Draw circle by scaling the 1x1 texture
                    spriteBatch.Draw(
                        _lineTexture,
                        _position,
                        null,
                        circleColor,
                        0.0f,
                        new Vector2(0.5f, 0.5f), // Center the circle
                        new Vector2(circleRadius * 2, circleRadius * 2),
                        SpriteEffects.None,
                        0.0f
                    );
                }
                else
                {
                    // Draw sword as a line/rectangle like the player's equipped sword
                    float weaponLength = 75.0f; // Same as player's sword
                    float weaponThickness = 8.0f; // Same as player's sword
                    
                    Vector2 weaponStart = _position;
                    Vector2 weaponEnd = _position + new Vector2(weaponLength, 0);
                    
                    Vector2 edge = weaponEnd - weaponStart;
                    float angle = (float)Math.Atan2(edge.Y, edge.X);
                    float lineLength = edge.Length();
                    
                    // Use weapon color
                    Color weaponColor = _weapon.WeaponColor;
                    
                    spriteBatch.Draw(
                        _lineTexture,
                        weaponStart,
                        null,
                        weaponColor,
                        angle,
                        Vector2.Zero,
                        new Vector2(lineLength, weaponThickness),
                        SpriteEffects.None,
                        0.0f
                    );
                }
            }
        }
    }
}

