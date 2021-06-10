using System;
using System.Runtime.InteropServices;
using PInvoke;

namespace MuteApp
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct GUITHREADINFO
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }
}
