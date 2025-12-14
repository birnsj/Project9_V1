using Microsoft.Xna.Framework;

namespace Project9
{
    /// <summary>
    /// A sword weapon that deals 20 damage
    /// </summary>
    public class Sword : Weapon
    {
        public override float Damage => 20.0f;
        public override string Name => "Sword";
        public override Color WeaponColor => Color.Silver;
        public override float KnockbackDuration => 0.5f; // 0.5 seconds of stun/knockback
    }
}

