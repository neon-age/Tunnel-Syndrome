

using System;
using System.Runtime.InteropServices;


[StructLayout(LayoutKind.Sequential)]
public struct KBDLLHOOKSTRUCT
{
    public uint vkCode;
    public uint scanCode;
    public KBDLLHOOKSTRUCTFlags flags;
    public uint time;
    public UIntPtr dwExtraInfo;

    public static KBDLLHOOKSTRUCT CreateFromPtr (IntPtr ptr)
    {
        return (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(ptr, typeof(KBDLLHOOKSTRUCT));
    }
}

[Flags]
public enum KBDLLHOOKSTRUCTFlags : uint
{
    LLKHF_EXTENDED = 0x01,
    LLKHF_INJECTED = 0x10,
    LLKHF_ALTDOWN = 0x20,
    LLKHF_UP = 0x80,
}

public enum HookType : int
{
    WH_JOURNALRECORD = 0,
    WH_JOURNALPLAYBACK = 1,
    WH_KEYBOARD = 2,
    WH_GETMESSAGE = 3,
    WH_CALLWNDPROC = 4,
    WH_CBT = 5,
    WH_SYSMSGFILTER = 6,
    WH_MOUSE = 7,
    WH_HARDWARE = 8,
    WH_DEBUG = 9,
    WH_SHELL = 10,
    WH_FOREGROUNDIDLE = 11,
    WH_CALLWNDPROCRET = 12,
    WH_KEYBOARD_LL = 13,
    WH_MOUSE_LL = 14
}

public static class Win32API
{
    public delegate int HookProc (int code, IntPtr wParam, IntPtr lParam);

    [DllImport("User32")]
    public static extern IntPtr SetWindowsHookEx (HookType code, HookProc func, IntPtr hInstance, uint threadID);
    [DllImport("User32")]
    public static extern int UnhookWindowsHookEx (IntPtr hhook);
    [DllImport("User32")]
    public static extern int CallNextHookEx (IntPtr hhook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("Kernel32")]
    public static extern uint GetCurrentThreadId ();
    [DllImport("Kernel32")]
    public static extern IntPtr GetModuleHandle (string lpModuleName);
}