using Microsoft.Xna.Framework;

namespace Project9
{
    /// <summary>
    /// A gun weapon that fires projectiles
    /// </summary>
    public class Gun : Weapon
    {
        public override float Damage => 15.0f; // Damage per bullet
        public override string Name => "Gun";
        public override Color WeaponColor => Color.DarkGray;
        public override float KnockbackDuration => 0.4f; // 0.4 seconds of stun/knockback
        
        /// <summary>
        /// Projectile speed in pixels per second
        /// </summary>
        public float ProjectileSpeed => 500.0f;
        
        /// <summary>
        /// Fire rate in shots per second
        /// </summary>
        public float FireRate => 6.0f; // 6 shots per second (faster firing)
        
        /// <summary>
        /// Cooldown between shots in seconds
        /// </summary>
        public float FireCooldown => 1.0f / FireRate; // ~0.167 seconds
    }
}

