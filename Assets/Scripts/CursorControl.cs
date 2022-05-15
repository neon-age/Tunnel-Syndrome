using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class CursorControl : MonoBehaviour
{
    

    public static void MouseEvent(MouseFlags value)
    {
        MousePoint position = GetCursorPosition();
        mouse_event((int)value, position.X, position.Y, 0, 0);
    }
    public static void MouseMoveEvent(Vector2 position)
    {
        mouse_event((int)MouseFlags.Absolute, (int)position.x, (int)position.y, 0, 0);
    }
    public static MousePoint GetCursorPosition()
    {
        MousePoint currentMousePoint;
        var gotPoint = GetCursorPos(out currentMousePoint);
        if (!gotPoint) { currentMousePoint = new MousePoint() { X = 0, Y = 0 }; }
        return currentMousePoint;
    }
    [Serializable] public struct ButtonPress
    {
        public enum State { None = 0, Down = 1, Hold = 2, Up = 4, }
        public State state;
        public bool isDownOrHold => state == State.Down || state == State.Hold;
        public bool isDown => state == State.Down;
        public bool isHold => state == State.Hold;
        public bool isUp => state == State.Up;

        public override string ToString() => state.ToString();
        public void UpdateFromBool(bool b)
        {
            if (state == State.None &&  b) state = State.Down;
            else if (state == State.Down &&  b) state = State.Hold;
            else if (state == State.Hold && !b) state = State.Up;
            else if (state == State.Up   && !b) state = 0;
        }
    }
    
     

    static protected IntPtr keyHookPtr = IntPtr.Zero;
    static protected IntPtr mouseHookPtr = IntPtr.Zero;

    static bool InterceptMessages = true;
    private static bool SetHook()
    {
        if (keyHookPtr == IntPtr.Zero)
        {
           keyHookPtr = Win32API.SetWindowsHookEx(HookType.WH_KEYBOARD_LL, HandleLowLevelHookProc, IntPtr.Zero, 0);
           //mouseHookPtr = Win32API.SetWindowsHookEx(HookType.WH_MOUSE_LL, HandleMouseLowLevelHookProc, IntPtr.Zero, 0);
           return true;
        }
        return false;
    }
    private static void RemoveHook ()
    {
        if (keyHookPtr != IntPtr.Zero) { Win32API.UnhookWindowsHookEx(keyHookPtr); keyHookPtr = IntPtr.Zero; }
        //if (mouseHookPtr != IntPtr.Zero) { Win32API.UnhookWindowsHookEx(mouseHookPtr); mouseHookPtr = IntPtr.Zero; }
    }
    
    [AOT.MonoPInvokeCallback(typeof(Win32API.HookProc))]
    private static int HandleMouseLowLevelHookProc (int code, IntPtr wParam, IntPtr lParam)
    {
        Debug.Log(code);
        if (code == 0x20a) {
            Debug.Log("mouse wheel");
            // WM_MOUSEWHEEL, find the control at screen position m.LParam
            Point pos = new Point(lParam.ToInt32() & 0xffff, lParam.ToInt32() >> 16);
            Debug.Log(pos);
            IntPtr hWnd = WindowFromPoint(pos);
            if (hWnd != IntPtr.Zero) {
                SendMessage(hWnd, code, wParam, lParam);
                return 1;
            }
        }
        return Win32API.CallNextHookEx(mouseHookPtr, 0, wParam, lParam);
    }
    /*
    [AOT.MonoPInvokeCallback(typeof(Win32API.HookProc))]
    private static int HandleLowLevelHookProc (int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0) return Win32API.CallNextHookEx(keyHookPtr, code, wParam, lParam);

        var kbd = KBDLLHOOKSTRUCT.CreateFromPtr(lParam);
        var keyState = (RawKeyState)wParam;
        var key = (RawKey)kbd.vkCode;

        if (keyState == RawKeyState.KeyDown || keyState == RawKeyState.SysKeyDown) 
            HandleKeyDown(key);
        else 
            HandleKeyUp(key);

        InterceptMessages = interceptKeys.Contains(key);
        return InterceptMessages ? 1 : Win32API.CallNextHookEx(keyHookPtr, 0, wParam, lParam);
    }*/
    
    [AOT.MonoPInvokeCallback(typeof(Win32API.HookProc))]
    private static int HandleLowLevelHookProc (int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var kbd = KBDLLHOOKSTRUCT.CreateFromPtr(lParam);
            var keyState = (RawKeyState)wParam;
            var key = (RawKey)kbd.vkCode;

            // https://www.codeproject.com/Articles/1273010/Global-Hotkeys-within-Desktop-Applications
            ThreadPool.QueueUserWorkItem(HandleSingleKeyboardInput, (kbd, keyState));

            if (InterceptMessages && interceptKeys.Contains(key))
                return 1;
        }
        return Win32API.CallNextHookEx(keyHookPtr, code, wParam, lParam);
    }

    const int WH_KEYBOARD_LL = 13;

    static void HandleSingleKeyboardInput(object param)
    {
        var (str, state) = ((KBDLLHOOKSTRUCT, RawKeyState))param;
        var key = (RawKey)str.vkCode;
        var modifier = GetModifierKeyFromCode(str.vkCode);

        if (state == RawKeyState.KeyDown || state == RawKeyState.SysKeyDown)
        {
            if (modifier != 0)
                heldMods.Add(modifier);

            HandleKeyDown(key);
        }
        else
        {
            if (modifier != 0)
                heldMods.Remove(modifier);

            HandleKeyUp(key);
        }
    }

    [Flags]
    public enum ModKey
    {
        Alt = 1, Control = 2, Shift = 4, WindowsKey = 8,
    }

    public static ModKey GetModifierKeyFromCode(uint keyCode)
    {
        switch (keyCode)
        {
            case 0xA0: case 0xA1: case 0x10: return ModKey.Shift;
            case 0xA2: case 0xA3: case 0x11: return ModKey.Control;
            case 0x12: case 0xA4: case 0xA5: return ModKey.Alt;
            case 0x5B: case 0x5C: return ModKey.WindowsKey;
            default: return 0;
        }
    }

    static Action<RawKey, bool> onKey;
    static Action<RawKey> onKeyDown;
    static Action<RawKey> onKeyUp;
    static HashSet<RawKey> heldKeys = new HashSet<RawKey>();
    static HashSet<ModKey> heldMods = new HashSet<ModKey>();

    private static void HandleKeyDown (RawKey key)
    {
        var added = heldKeys.Add(key);
        if (added)
        {
            onKeyDown?.Invoke(key);
            onKey?.Invoke(key, true);
            //Debug.LogError($"{key} down");
        }
    }

    private static void HandleKeyUp (RawKey key)
    {
        heldKeys.Remove(key);
        onKeyUp?.Invoke(key);
        onKey?.Invoke(key, false);
        //Debug.LogError($"{key} up");
    }
     
    Vector2 joyMove;
    public void SendMoveInput(InputAction.CallbackContext ctx)
    {
        joyMove = ctx.ReadValue<Vector2>();
        joyMove.y = -joyMove.y;
        //Debug.LogError($"{joyMove}");//
    }
    
    bool joyLeftClick, joyRightClick, joyMiddleClick;
    public void SendLeftClick(InputAction.CallbackContext ctx)
    {
        joyLeftClick = ctx.ReadValue<float>() != 0;
    }
    public void SendRightClick(InputAction.CallbackContext ctx)
    {
        joyRightClick = ctx.ReadValue<float>() != 0;
    }
    public void SendMiddleClick(InputAction.CallbackContext ctx)
    {
        joyMiddleClick = ctx.ReadValue<float>() != 0;
    }

    public float mouseSensitivity = 1;
    public void SetMouseSensitivity(float value) => mouseSensitivity = value;
    public float joySensitivity = 1;
    public void SetJoySensitivity(float value) => joySensitivity = value;
    public float accel = 1;
    public void SetAcceleration(float value) => accel = value;

    public InputStickProcessor stickProcessor;
    public bool keyboardHook;
    public float altIdleMinTime;
    public float altIdleMaxTime;

    InputStickProcessor.Stick stick;
    Vector2 cursorPos;
    ButtonPress leftClick, rightClick, middleClick;
    bool leftClickHold, rightClickHold, middleClickHold;
    
    static HashSet<RawKey> interceptKeys = new HashSet<RawKey>() { 
        RawKey.F1,
        RawKey.Numpad8, RawKey.Numpad5, RawKey.Numpad4, RawKey.Numpad6, 
        RawKey.Numpad0, RawKey.Numpad1, RawKey.Numpad2 };

    public void SetKeyboardHook(bool hook)
    {
        keyboardHook = hook;
        if (hook) SetHook(); else RemoveHook();
    }
    
    private void Start()
    {
        if (keyboardHook)
            SetHook();
        Debug.Log("set hook");
        cursorPos = GetCursorPosition();
        onKey += OnTab;
        //onKeyDown += _ => UpdateInputs();
        //onKeyUp += _ => UpdateInputs();
    }
    
    void OnDisable(){
        Debug.Log("remove hook");
        RemoveHook();
    }

    bool cursorIdle;
    Vector2 moveDelta;
    void UpdateInputs()
    {
        var w = IsHeld(RawKey.Numpad8);
        var s = IsHeld(RawKey.Numpad5);
        var a = IsHeld(RawKey.Numpad4);
        var d = IsHeld(RawKey.Numpad6);
        
        leftClickHold = IsHeld(RawKey.Numpad0) || IsHeld(RawKey.F1);
        rightClickHold = IsHeld(RawKey.Numpad1);
        middleClickHold = IsHeld(RawKey.Numpad2);
        
        var wasd = new Vector2(
            a || d ? (a ? -1 : 1) : 0, 
            w || s ? (w ? -1 : 1) : 0);
        wasd = Vector2.ClampMagnitude(wasd, 1);
        wasd *= mouseSensitivity;

        //if (joyMove.magnitude < 0.1f)
        //    joyMove = default;

        moveDelta = joyMove * joySensitivity + wasd;
        
        //MouseMoveEvent(Vector2.zero);
        
        //if (Input.GetButtonDown("Fire1")) leftClickHold = true;
        //if (Input.GetButtonUp("Fire1")) leftClickHold = false; 
    }

    float cursorIdleTime;
    ButtonPress altIdleClick;
    bool altIdleDown;
    bool altTab;

    bool IsHeld(RawKey key) => heldKeys.Contains(key);

    void OnTab(RawKey key, bool down)
    {
        if (key == RawKey.Tab && heldMods.Contains(ModKey.Alt))
            altTab = true;
    }

    void FixedUpdate()
    {
        UpdateInputs();

        stick.raw = moveDelta;
        stickProcessor.ProcessStick(ref stick, accel);

        var newCursorPos = (Vector2)GetCursorPosition();
        var cursorMoved = cursorPos != newCursorPos;
        cursorPos = newCursorPos;

        cursorIdleTime += Time.deltaTime;

        if (stick.value.magnitude > 0.1f){
            cursorPos += stick.value;
            SetCursorPos((int)cursorPos.x, (int)cursorPos.y);
            //MouseEvent(MouseEventFlags.Move);
        }

        bool isAltHeld = heldMods.Contains(ModKey.Alt);

        if (!isAltHeld)
            altTab = false;


        if (altTab && cursorIdleTime > altIdleMinTime)
        {
            Debug.Log("down");
            altIdleDown = true;
        }

        bool altIdleGap = cursorIdleTime > altIdleMinTime && cursorIdleTime < altIdleMaxTime;

        //Debug.Log(altIdleMinTime > cursorIdleTime);
        Debug.Log(altIdleGap);

        //if (altIdleDown && cursorIdleTime < altIdleMaxTime && (altIdleMinTime > cursorIdleTime || !altTab))
        //{
        //    altIdleDown = false;
        //    Debug.Log("up");
        //}
        if (altIdleDown && (altIdleMinTime > cursorIdleTime || !altTab))
        {
            altIdleDown = false;
            //altIdleClick.state = ButtonPress.State.Up;
            Debug.Log("up");
        }
        //altIdleClick.UpdateFromBool(altIdleDown);

        //if (isAltIdleTime && altIdleClick.state != 0)
        //    altIdleClick.state = ButtonPress.State.Up;

        //Debug.Log($"{cursorIdleTime} {altIdleGap}");
        //Debug.Log($"{isAltHeld} {altIdleMoved} {altIdleClick} {cursorIdleTime}");
        //Debug.Log($"{idleState} {altClick}");

        if (cursorMoved) cursorIdleTime = 0;

        leftClick.UpdateFromBool(leftClickHold || joyLeftClick);
        
        if (altIdleClick.isDown || leftClick.isDown) MouseEvent(MouseFlags.LeftDown);
        if (altIdleClick.isUp || leftClick.isUp){
            altTab = false;
            altIdleClick.state = 0;
            MouseEvent(MouseFlags.LeftUp);
        }
        
        rightClick.UpdateFromBool(rightClickHold || joyRightClick);
        
        if (rightClick.isDown) MouseEvent(MouseFlags.RightDown);
        if (rightClick.isUp) MouseEvent(MouseFlags.RightUp); 
        
        middleClick.UpdateFromBool(middleClickHold || joyMiddleClick);
        
        if (middleClick.isDown) MouseEvent(MouseFlags.MiddleDown);
        if (middleClick.isUp) MouseEvent(MouseFlags.MiddleUp);
    }


    [AOT.MonoPInvokeCallback(typeof(Win32API.HookProc))]
    private static int HandleHookProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0) return Win32API.CallNextHookEx(keyHookPtr, code, wParam, lParam);

        var isKeyDown = ((int)lParam & (1 << 31)) == 0;
        var key = (RawKey)wParam;

        if (isKeyDown) HandleKeyDown(key);
        else HandleKeyUp(key);

        return InterceptMessages ? 1 : Win32API.CallNextHookEx(keyHookPtr, 0, wParam, lParam);
    }

    [Flags]
    public enum MouseFlags
    {
        LeftDown = 0x00000002, LeftUp = 0x00000004,
        MiddleDown = 0x00000020, MiddleUp = 0x00000040,
        RightDown = 0x00000008, RightUp = 0x00000010,
        Move = 0x00000001, Absolute = 0x00008000,
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct MousePoint
    {
        public int X;
        public int Y;
        public static implicit operator Vector2(MousePoint p) => new Vector2(p.X, p.Y);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point pt);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

    [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out MousePoint lpMousePoint);

    [DllImport("user32.dll")]
    private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

    [DllImport("user32")]
    protected static extern IntPtr SetWindowsHookEx(
         HookType code, HookProc func, IntPtr hInstance, int threadID);
    [DllImport("user32")]
    protected static extern int UnhookWindowsHookEx(
        IntPtr hhook);
    [DllImport("user32")]
    protected static extern int CallNextHookEx(
        IntPtr hhook, int code, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);

    protected delegate int HookProc(int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);
}
