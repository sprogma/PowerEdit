using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;

namespace EditorFramework.Events
{
    public record KeyBindingItem
    {

        public readonly KeyCode Key;
        public readonly KeyMode Mode;
        public readonly KeyMode ModeMask;

        public KeyBindingItem(
                KeyCode Key,
                KeyMode Mode = KeyMode.None,
                KeyMode ModeMask = KeyMode.LeftShift
                                   | KeyMode.RightShift
                                   | KeyMode.LeftCtrl
                                   | KeyMode.RightCtrl
                                   | KeyMode.LeftAlt
                                   | KeyMode.RightAlt)
        {
            ModeMask |= Mode;
            if (ModeMask.HasFlag(KeyMode.Shift))
            {
                ModeMask &= ~(KeyMode.LeftShift | KeyMode.RightShift);
            }
            if (ModeMask.HasFlag(KeyMode.Ctrl))
            {
                ModeMask &= ~(KeyMode.LeftCtrl | KeyMode.RightCtrl);
            }
            if (ModeMask.HasFlag(KeyMode.Alt))
            {
                ModeMask &= ~(KeyMode.LeftAlt | KeyMode.RightAlt);
            }
            if (ModeMask.HasFlag(KeyMode.Win))
            {
                ModeMask &= ~(KeyMode.LeftWin | KeyMode.RightWin);
            }
            this.Key = Key;
            this.Mode = Mode;
            this.ModeMask = ModeMask;
        }
    }

    public static class ChordExtensions
    {
        extension(KeyChordEvent chord)
        {
            public KeyEvent LastKey => chord.Keys[^1];

            public bool IsNoShift(KeyCode key, KeyMode mode = KeyMode.None, bool IgnoreKeyUp = true)
            {
                return chord.Is(new KeyBindingItem(key, mode, KeyMode.LeftCtrl | KeyMode.RightCtrl | KeyMode.LeftAlt | KeyMode.RightAlt), IgnoreKeyUp);
            }

            public bool Is(KeyCode key, KeyMode mode = KeyMode.None, bool IgnoreKeyUp = true)
            {
                return chord.Is(new KeyBindingItem(key, mode), IgnoreKeyUp);
            }

            public bool Is(KeyBindingItem key, bool IgnoreKeyUp = true)
            {
                return chord.Is([key], IgnoreKeyUp);
            }

            public bool Is(KeyBindingItem[] items, bool IgnoreKeyUp = true)
            {
                var data = IgnoreKeyUp ? chord.Keys.OfType<KeyDownEvent>().ToArray() : chord.Keys;
                if (data.Length < items.Length)
                {
                    return false;
                }
                return data.TakeLast(items.Length)
                           .Zip(items)
                           .All(x => x.First.Key == x.Second.Key &&
                                     (x.First.Mode & x.Second.ModeMask) == x.Second.Mode);
            }
        }
    }

    public struct KeyBinding(KeyBindingItem[] items, bool ignoreKeyUp = true)
    {
        public KeyBindingItem[] Items = items;
        public bool IgnoreKeyUp = ignoreKeyUp;
    }
}
