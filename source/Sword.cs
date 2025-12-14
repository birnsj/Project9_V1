using Microsoft.Xna.Framework;

namespace Project9
{
    /// <summary>
    /// A sword weapon that deals melee damage
    /// </summary>
    public class Sword : Weapon
    {
        public Sword()
        {
            // Default values
            Damage = 20.0f;
            Name = "Sword";
            WeaponColor = Color.Silver;
            KnockbackDuration = 0.5f; // 0.5 seconds of stun/knockback
        }
        
        public Sword(float damage, string name, Color weaponColor, float knockbackDuration)
        {
            Damage = damage;
            Name = name;
            WeaponColor = weaponColor;
            KnockbackDuration = knockbackDuration;
        }
    }
}

