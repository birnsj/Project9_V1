using Microsoft.Xna.Framework;

namespace Project9
{
    /// <summary>
    /// A gun weapon that fires projectiles
    /// </summary>
    public class Gun : Weapon
    {
        /// <summary>
        /// Projectile speed in pixels per second
        /// </summary>
        public float ProjectileSpeed { get; set; }
        
        /// <summary>
        /// Fire rate in shots per second
        /// </summary>
        public float FireRate { get; set; }
        
        /// <summary>
        /// Cooldown between shots in seconds
        /// </summary>
        public float FireCooldown => 1.0f / FireRate; // Calculated from fire rate
        
        public Gun()
        {
            // Default values
            Damage = 15.0f; // Damage per bullet
            Name = "Gun";
            WeaponColor = Color.DarkGray;
            KnockbackDuration = 0.4f; // 0.4 seconds of stun/knockback
            ProjectileSpeed = 500.0f;
            FireRate = 6.0f; // 6 shots per second (faster firing)
        }
        
        public Gun(float damage, string name, Color weaponColor, float knockbackDuration, float projectileSpeed, float fireRate)
        {
            Damage = damage;
            Name = name;
            WeaponColor = weaponColor;
            KnockbackDuration = knockbackDuration;
            ProjectileSpeed = projectileSpeed;
            FireRate = fireRate;
        }
    }
}

