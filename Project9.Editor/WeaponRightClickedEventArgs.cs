using System;
using Project9.Shared;

namespace Project9.Editor
{
    /// <summary>
    /// Event arguments for weapon right-click
    /// </summary>
    public class WeaponRightClickedEventArgs : EventArgs
    {
        public WeaponData Weapon { get; }

        public WeaponRightClickedEventArgs(WeaponData weapon)
        {
            Weapon = weapon;
        }
    }
}

