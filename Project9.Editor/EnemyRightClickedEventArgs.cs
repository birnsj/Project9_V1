using System;
using Project9.Shared;

namespace Project9.Editor
{
    /// <summary>
    /// Event arguments for enemy right-click
    /// </summary>
    public class EnemyRightClickedEventArgs : EventArgs
    {
        public EnemyData Enemy { get; }

        public EnemyRightClickedEventArgs(EnemyData enemy)
        {
            Enemy = enemy;
        }
    }
}




