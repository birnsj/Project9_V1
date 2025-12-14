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
        public abstract float Damage { get; }

        /// <summary>
        /// The name of the weapon
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// The color used to render the weapon on the ground
        /// </summary>
        public abstract Color WeaponColor { get; }
        
        /// <summary>
        /// Knockback/stun duration in seconds when enemy is hit
        /// </summary>
        public abstract float KnockbackDuration { get; }
    }
}

