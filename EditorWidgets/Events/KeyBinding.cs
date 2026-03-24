using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;

namespace EditorFramework.Events
{
    public record KeyBindingItem(
        KeyCode Key,
        KeyMode Mode = KeyMode.None,
        KeyMode ModeMask = KeyMode.LeftShift
                           | KeyMode.RightShift
                           | KeyMode.LeftCtrl
                           | KeyMode.RightCtrl
                           | KeyMode.LeftAlt
                           | KeyMode.RightAlt)
    {

        public readonly KeyCode Key = Key;
        public readonly KeyMode Mode = Mode;
        public readonly KeyMode ModeMask = ModeMask | Mode;
    }

    public static class ChordExtensions
    {
        extension(KeyChordEvent chord)
        {
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

        // Расширение для сложных комбинаций (на будущее)
        public static bool Is(this Chord chord, KeyCode code, KeyMode mod1, KeyMode mod2)
        {
            return new KeyBinding([new KeyBindingItem(code, mod1, mod2)])
                   .Check(chord.Keys);
        }
    }

    public struct KeyBinding(KeyBindingItem[] items, bool ignoreKeyUp = true)
    {
        public KeyBindingItem[] Items = items;
        public bool IgnoreKeyUp = ignoreKeyUp;

        public readonly bool Check(IEnumerable<KeyEvent> events)
        {
        }
    }
}
