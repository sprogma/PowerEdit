using System;
using System.Collections.Generic;
using System.Text;

namespace EditorFramework.Events
{
    public enum KeyCode
    {
        // cmd
        Backspace = 0x08,
        Tab = 0x09,
        Clear = 0x0C,
        Enter = 0x0D,
        Pause = 0x13,
        CapsLock = 0x14,
        Escape = 0x1B,
        Space = 0x20,

        // mods
        Shift = 0x10,
        Control = 0x11,
        Alt = 0x12,
        LeftShift = 0xA0,
        RightShift = 0xA1,
        LeftControl = 0xA2,
        RightControl = 0xA3,
        LeftAlt = 0xA4,
        RightAlt = 0xA5,

        // nav
        PageUp = 0x21,
        PageDown = 0x22,
        End = 0x23,
        Home = 0x24,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        PrintScreen = 0x2C,
        Insert = 0x2D,
        Delete = 0x2E,
        Help = 0x2F,

        // digits [to get number use & 0xF]
        D1 = 0x31, 
        D2 = 0x32, 
        D3 = 0x33, 
        D4 = 0x34,
        D5 = 0x35, 
        D6 = 0x36, 
        D7 = 0x37, 
        D8 = 0x38, 
        D9 = 0x39, 
        D0 = 0x40,

        // keys
        A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45, F = 0x46, G = 0x47,
        H = 0x48, I = 0x49, J = 0x4A, K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E,
        O = 0x4F, P = 0x50, Q = 0x51, R = 0x52, S = 0x53, T = 0x54, U = 0x55,
        V = 0x56, W = 0x57, X = 0x58, Y = 0x59, Z = 0x5A,

        // symbols
        Minus = 0xD0,
        Equal = 0xD1,
        OpenBrackets = 0xD2,
        CloseBrackets = 0xD3,
        Semicolon = 0xBA,
        Backslash = 0xE2,
        Comma = 0xBC,
        Tilde = 0xC0,
        Period = 0xBE,
        Quotes = 0xDE,
        
        // system
        LeftWindows = 0x5B,
        RightWindows = 0x5C,
        Applications = 0x5D,
        Sleep = 0x5F,

        // numpad
        NumPad0 = 0x60, NumPad1 = 0x61, NumPad2 = 0x62, NumPad3 = 0x63,
        NumPad4 = 0x64, NumPad5 = 0x65, NumPad6 = 0x66, NumPad7 = 0x67,
        NumPad8 = 0x68, NumPad9 = 0x69,
        NumPadMultiply = 0x6A, NumPadAdd = 0x6B, NumPadSeparator = 0x6C,
        NumPadSubtract = 0x6D, NumPadDecimal = 0x6E, NumPadDivide = 0x6F,

        // fn
        F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73, F5 = 0x74, F6 = 0x75,
        F7 = 0x76, F8 = 0x77, F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B,
        F13 = 0x7C, F14 = 0x7D, F15 = 0x7E, F16 = 0x7F, F17 = 0x80, F18 = 0x81,
        F19 = 0x82, F20 = 0x83, F21 = 0x84, F22 = 0x85, F23 = 0x86, F24 = 0x87,

        // locks
        NumLock = 0x90,
        ScrollLock = 0x91,

        // OEM buttons
        OemPlus = 0xBB,
        OemQuestion = 0xBF,
        OemPipe = 0xDC,
        Oem8 = 0xDF,
    }

    [Flags]
    public enum KeyMode
    {
        None = 0x0,
        LeftShift = 0x1,
        RightShift = 0x2,
        LeftAlt = 0x4,
        RightAlt = 0x8,
        LeftCtrl = 0x10,
        RightCtrl = 0x20,
        LeftWin = 0x40,
        RightWin = 0x80,
        CapsLock = 0x100,
        NumLock = 0x200,
        Shift = 0x400,
        Ctrl = 0x800,
        Alt = 0x1000,
        Win = 0x2000,
    }

    [Flags]
    public enum MouseButton
    {
        Left = 0x1,
        Middle = 0x2,
        Right = 0x4,
    }

    public abstract record EventBase()
    {
        public DateTime Timestamp = DateTime.UtcNow;
    }
    public record QuitEvent : EventBase;

    public record TextInputEvent(
        byte[] Text
    ) : EventBase;

    public record KeyEvent(
        KeyCode Key,
        KeyMode Mode
    ) : EventBase
    {
        public readonly KeyCode Key = Key;
        public readonly KeyMode Mode = Mode 
            | (Mode.HasFlag(KeyMode.LeftShift) || Mode.HasFlag(KeyMode.RightShift) ? KeyMode.Shift : KeyMode.None)
            | (Mode.HasFlag(KeyMode.LeftCtrl) || Mode.HasFlag(KeyMode.RightCtrl) ? KeyMode.Ctrl : KeyMode.None)
            | (Mode.HasFlag(KeyMode.LeftAlt) || Mode.HasFlag(KeyMode.RightAlt) ? KeyMode.Alt : KeyMode.None)
            | (Mode.HasFlag(KeyMode.LeftWin) || Mode.HasFlag(KeyMode.RightWin) ? KeyMode.Win : KeyMode.None);
    }

    public record KeyDownEvent(
        KeyCode Key,
        KeyMode Mode
    ) : KeyEvent(Key, Mode);

    public record KeyUpEvent(
        KeyCode Key,
        KeyMode Mode
    ) : KeyEvent(Key, Mode);

    public record KeyChordEvent(
        KeyEvent[] Keys
    ) : EventBase;

    public record PasteEvent(
        string Text
    ) : EventBase;

    public record MouseEvent(
        long X,
        long Y,
        MouseButton State
    ) : EventBase;

    public record MouseMoveEvent(
        long X,
        long Y,
        MouseButton State,
        long Xrel,
        long Yrel
    ) : MouseEvent(X, Y, State);

    public record MouseWheelEvent(
        long X,
        long Y,
        MouseButton State,
        long Xdelta,
        long Ydelta
    ) : MouseEvent(X, Y, State);

    public record MouseClickEvent(
        long X,
        long Y,
        MouseButton State,
        MouseButton Button
    ) : MouseEvent(X, Y, State);
  
    public record MouseDownEvent(
        long X,
        long Y,
        MouseButton State,
        MouseButton Button
    ) : MouseClickEvent(X, Y, State, Button);

    public record MouseUpEvent(
        long X,
        long Y,
        MouseButton State,
        MouseButton Button
    ) : MouseClickEvent(X, Y, State, Button);

    public record MouseSequenceEvent(
        MouseClickEvent[] Clicks
    ) : EventBase;
}
