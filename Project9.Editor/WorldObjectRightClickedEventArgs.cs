using Project9.Shared;

namespace Project9.Editor
{
    /// <summary>
    /// Event arguments for when a world object is right-clicked or left-clicked
    /// </summary>
    public class WorldObjectRightClickedEventArgs : EventArgs
    {
        public WorldObject WorldObject { get; }

        public WorldObjectRightClickedEventArgs(WorldObject worldObject)
        {
            WorldObject = worldObject;
        }
    }
}







