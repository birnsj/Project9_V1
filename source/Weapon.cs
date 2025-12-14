using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    /// <summary>
    /// Base class for all weapons in the game
    /// </summary>
    public abstract class Weapon
    {
        /// <summary>
        /// The damage this weapon deals
        /// </summary>
        public float Damage { get; set; }

        /// <summary>
        /// The name of the weapon
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// The color used to render the weapon on the ground
        /// </summary>
        public Color WeaponColor { get; set; }
        
        /// <summary>
        /// Knockback/stun duration in seconds when enemy is hit
        /// </summary>
        public float KnockbackDuration { get; set; }
    }
}

