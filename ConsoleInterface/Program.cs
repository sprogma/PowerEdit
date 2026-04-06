using CommandProviderInterface;
using ConsoleInterface;
using EditorCore.Buffer;
using EditorCore.File;
using EditorCore.Selection;
using EditorCore.Server;
using EditorFramework;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using EditorFramework.Widgets;
using Common;
using Lsp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PowershellCommandProvider;
using PythonCommandProvider;
using RegexTokenizer;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using TextBuffer;

namespace SDL2Interface
{
    internal class App : IApplication
    {
        public static List<BaseWindow> windows = [];

        public static void OpenWindow(BaseWindow window)
        {
            windows.Add(window);
        }

        internal static void RaiseWindow(BaseWindow window)
        {
            windows.Remove(window);
            windows.Add(window);
        }

        public void SetClipboard(string text)
        {
            ConsoleCanvas.SetClipboard(text);
        }

        public void Main(string[] raw_args)
        {
            Logger.Log(LogLevel.AppStart, "Run console interface");
            List<string> args = [.. raw_args];

            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            List<string?> fileToOpen = [.. args.Where(x => !x.StartsWith("-")).Cast<string?>()];
            if (fileToOpen.Count == 0)
            {
                fileToOpen.Add(null);
            }
            //fileToOpen = ["D:\\big.txt"];
            //fileToOpen = ["C:\\Users\\User\\AppData\\Local\\Temp\\copy_10gb.txt"];
            //fileToOpen = ["C:\\Users\\User\\AppData\\Local\\Temp\\big.txt"]; // too big for now
            Logger.Log($"Opening files {fileToOpen.Count} \"{fileToOpen}\"...");
            
            using Render render = new(new EditorFramework.ColorTheme());

            /* create application instance */
            ICommandProvider? provider;
                
            if (args.Contains("--python"))
            {
                args.Remove("--python");
                provider = new PythonProvider();
            }
            else
            {
                provider = new PowershellProvider();
            }

            if (provider == null)
            {
                Logger.Log(LogLevel.Error, "Not selected any provider");
                return;
            }

            FileTabsWindow tabs = new(this, GetLayout<FileTabsWindow>.Value, []);
            EditorServer server = new(provider)
            {
                UseLSP = args.Contains("--lsp")
            };

            // add "lsp" module
            if (args.Contains("--linter"))
            {
                SimpleLinterMod.Init(server);
            }

            // start project widget
            ProjectEditorWindow project = new(
                this,
                GetLayout<ProjectEditorWindow>.Value,
                server,
                tabs.OpenFile,
                tabs.RaiseFile,
                tabs
            );

            windows.Add(project);

            foreach (var file in fileToOpen)
            {
                if (file != null)
                {
                    project.OpenFile(file);
                }
                else
                {
                    project.CreateFile(null, "c");
                }
            }

            if (!args.Contains("--no-interactive"))
            {
                EventManager pool = new((e) =>
                {
                    foreach (var win in windows.Reverse<BaseWindow>())
                    {
                        if (!win.Event(e))
                        {
                            return false;
                        }
                    }
                    return true;
                });
                while (windows.Count > 0)
                {
                    foreach (var win in windows)
                    {
                        render.Draw(win);
                    }
                    render.Canvas.Flush();

                    while (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);

                        if (char.IsHighSurrogate(key.KeyChar))
                        {
                            var nextkey = Console.ReadKey(true);
                            Debug.Assert(char.IsLowSurrogate(nextkey.KeyChar));
                            string surrogatePair = new([key.KeyChar, nextkey.KeyChar]);
                            pool.AddEvent(new TextInputEvent(Encoding.UTF8.GetBytes(surrogatePair)));
                            continue;
                        }
                        Logger.Log($"Key {key.Key} char={(int)key.KeyChar}");

                        if (key.KeyChar == 27)
                        {
                            var captured = new List<ConsoleKeyInfo> { key };

                            var sw = Stopwatch.StartNew();
                            while (sw.ElapsedMilliseconds < 30 && !Console.KeyAvailable)
                            {
                                Thread.Sleep(5);
                            }

                            while (Console.KeyAvailable && captured.Count < 6) // get full message
                            {
                                captured.Add(Console.ReadKey(true));
                            }

                            string tail = string.Concat(captured.Skip(1).Select(k => k.KeyChar));
                            Logger.Log($"Got initial paste, got end as {tail}");
                            if (tail == "[200~")
                            {
                                var pasteBuffer = new StringBuilder();
                                while (true)
                                {
                                    var pKey = Console.ReadKey(true);
                                    if (pKey.Key == ConsoleKey.Escape)
                                    {
                                        // check if it is "[201~"
                                        var endSw = Stopwatch.StartNew();
                                        while (endSw.ElapsedMilliseconds < 10 && !Console.KeyAvailable) Thread.Sleep(1);

                                        string endTail = "";
                                        while (Console.KeyAvailable) endTail += Console.ReadKey(true).KeyChar;

                                        if (endTail == "[201~") break;
                                        pasteBuffer.Append('\x1b').Append(endTail);
                                    }
                                    else pasteBuffer.Append(pKey.KeyChar);
                                }
                                pool.AddEvent(new PasteEvent(pasteBuffer.ToString()));
                                pool.ProcessEvents();
                                continue;
                            }

                            // send all events
                            foreach (var k in captured)
                            {
                                foreach (var e in CreateEvent(k)) pool.AddEvent(e);
                            }
                            pool.ProcessEvents();
                            continue;
                        }

                        foreach (var e in CreateEvent(key))
                        {
                            pool.AddEvent(e);
                        }
                        pool.ProcessEvents();
                    }

                    Thread.Sleep(10);
                }
            }
        }

        KeyMode Convert(ConsoleModifiers key)
        {
            KeyMode mode = KeyMode.None;

            if ((key & ConsoleModifiers.Shift) != 0) mode |= KeyMode.LeftShift;
            if ((key & ConsoleModifiers.Control) != 0) mode |= KeyMode.LeftCtrl;
            if ((key & ConsoleModifiers.Alt) != 0) mode |= KeyMode.LeftAlt;

            #if Windows
            if (Console.CapsLock) mode |= KeyMode.CapsLock;
            if (Console.NumberLock) mode |= KeyMode.NumLock;
            #endif

            return mode;
        }


        KeyCode Convert(ConsoleKey key)
        {
            return key switch
            {
                // Letters A-Z
                >= ConsoleKey.A and <= ConsoleKey.Z
                    => (KeyCode)((int)KeyCode.A + (key - ConsoleKey.A)),

                // Digits D1-D9
                >= ConsoleKey.D1 and <= ConsoleKey.D9
                    => (KeyCode)((int)KeyCode.D1 + (key - ConsoleKey.D0)),

                ConsoleKey.D0 => KeyCode.D0,

                // F-клавиши
                >= ConsoleKey.F1 and <= ConsoleKey.F12
                    => (KeyCode)((int)KeyCode.F1 + (key - ConsoleKey.F1)),
                >= ConsoleKey.F13 and <= ConsoleKey.F24
                    => (KeyCode)((int)KeyCode.F13 + (key - ConsoleKey.F13)),

                // Numpad Digits
                >= ConsoleKey.NumPad0 and <= ConsoleKey.NumPad0
                    => (KeyCode)((int)KeyCode.NumPad0 + (key - ConsoleKey.NumPad0)),

                // Numpad Ops
                ConsoleKey.Multiply => KeyCode.NumPadMultiply,
                ConsoleKey.Add => KeyCode.NumPadAdd,
                ConsoleKey.Subtract => KeyCode.NumPadSubtract,
                ConsoleKey.Decimal => KeyCode.NumPadDecimal,
                ConsoleKey.Divide => KeyCode.NumPadDivide,

                // Nav & System
                ConsoleKey.Enter => KeyCode.Enter,
                ConsoleKey.Escape => KeyCode.Escape,
                ConsoleKey.Backspace => KeyCode.Backspace,
                ConsoleKey.Tab => KeyCode.Tab,
                ConsoleKey.Spacebar => KeyCode.Space,
                ConsoleKey.Pause => KeyCode.Pause,
                ConsoleKey.PrintScreen => KeyCode.PrintScreen,
                ConsoleKey.Insert => KeyCode.Insert,
                ConsoleKey.Delete => KeyCode.Delete,
                ConsoleKey.Home => KeyCode.Home,
                ConsoleKey.End => KeyCode.End,
                ConsoleKey.PageUp => KeyCode.PageUp,
                ConsoleKey.PageDown => KeyCode.PageDown,
                ConsoleKey.Help => KeyCode.Help,
                ConsoleKey.RightArrow => KeyCode.Right,
                ConsoleKey.LeftArrow => KeyCode.Left,
                ConsoleKey.DownArrow => KeyCode.Down,
                ConsoleKey.UpArrow => KeyCode.Up,

                // Mods
                ConsoleKey.LeftWindows => KeyCode.LeftWindows,
                ConsoleKey.RightWindows => KeyCode.RightWindows,
                ConsoleKey.Applications => KeyCode.Applications,
                ConsoleKey.Sleep => KeyCode.Sleep,

                // Symbols & OEM
                ConsoleKey.OemMinus => KeyCode.Minus,
                ConsoleKey.OemPlus => KeyCode.Equal,
                ConsoleKey.Oem4 => KeyCode.OpenBrackets,
                ConsoleKey.Oem6 => KeyCode.CloseBrackets,
                ConsoleKey.Oem1 => KeyCode.Semicolon,
                ConsoleKey.Oem7 => KeyCode.Quotes,
                ConsoleKey.Oem3 => KeyCode.Tilde,
                ConsoleKey.OemComma => KeyCode.Comma,
                ConsoleKey.OemPeriod => KeyCode.Period,
                ConsoleKey.Oem2 => KeyCode.OemQuestion,
                ConsoleKey.Oem5 => KeyCode.Backslash,
                ConsoleKey.Oem102 => KeyCode.Backslash,

                _ => 0
            };
        }

        private IEnumerable<EventBase> CreateEvent(ConsoleKeyInfo evt)
        {
            bool isAlt = (evt.Modifiers & ConsoleModifiers.Alt) != 0;
            bool isCtrl = (evt.Modifiers & ConsoleModifiers.Control) != 0;
            if (evt.KeyChar != '\0' && !char.IsControl(evt.KeyChar) && !isAlt && !isCtrl)
            {
                return [
                    new TextInputEvent(Encoding.UTF8.GetBytes([evt.KeyChar])),
                    new KeyDownEvent(Convert(evt.Key), Convert(evt.Modifiers))
                ];
            }
            else
            {
                return [new KeyDownEvent(Convert(evt.Key), Convert(evt.Modifiers))];
            }
        }

        public void RemoveWindow(BaseWindow window)
        {
            windows.Remove(window);
        }

        public IEnumerable<BaseWindow> ListWindows()
        {
            return windows;
        }
    }

    internal class Program
    {
        static void Main(string[] raw_args)
        {
            App app = new();
            app.Main(raw_args);
        }
    }
}
