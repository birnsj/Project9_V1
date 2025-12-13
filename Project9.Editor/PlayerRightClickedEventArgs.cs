using System;
using Project9.Shared;

namespace Project9.Editor
{
    public class PlayerRightClickedEventArgs : EventArgs
    {
        public PlayerData Player { get; }

        public PlayerRightClickedEventArgs(PlayerData player)
        {
            Player = player;
        }
    }
}



