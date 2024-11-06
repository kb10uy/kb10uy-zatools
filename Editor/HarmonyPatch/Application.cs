using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public static class Application
    {
        public static extern bool isPlaying
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            get;
        }

        public static extern string dataPath
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            get;
        }
    }
}
