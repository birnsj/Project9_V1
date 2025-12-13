using System;
using Project9.Shared;

namespace Project9.Editor
{
    public class CameraRightClickedEventArgs : EventArgs
    {
        public CameraData Camera { get; }
        public int Index { get; }

        public CameraRightClickedEventArgs(CameraData camera, int index)
        {
            Camera = camera;
            Index = index;
        }
    }
}

