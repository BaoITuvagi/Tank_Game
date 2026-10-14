using UnityEngine;
namespace Tank  {
    public static class GameWindowFocus  {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern System.IntPtr GetForegroundWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(System.IntPtr window,out uint process);
        static readonly uint ProcessId=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;
#endif
        public static bool IsForeground  {
            get {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                uint pid;
                GetWindowThreadProcessId(GetForegroundWindow(),out pid);
                return pid==ProcessId;
#else
                return Application.isFocused;
#endif
            }
        }
    }
}
