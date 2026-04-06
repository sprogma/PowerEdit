using CommandProviderInterface;
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
using SDL_Sharp;
using SDL_Sharp.Ttf;
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

        /// <summary>
        /// Helping function to get TextInputEvent value
        /// </summary>
        /// <param name="e">TextInputEvent value to read</param>
        /// <returns> input string </returns>
        internal unsafe string GetTextInputValue(SDL_Sharp.TextInputEvent e)
        {
            byte* p = e.Text;
            int len = 0;
            while (p[len] != 0) len++;
            return Encoding.UTF8.GetString(p, len);
        }

        /// <summary>
        /// Helping function to get TextInputEvent value
        /// </summary>
        /// <param name="e">TextInputEvent value to read</param>
        /// <returns> input bytes</returns>
        internal unsafe byte[] GetTextInputBytes(SDL_Sharp.TextInputEvent e)
        {
            byte* p = e.Text;
            if (p == null) return [];
            int len = 0;
            while (p[len] != 0) len++;
            byte[] result = new byte[len];
            Marshal.Copy((IntPtr)p, result, 0, len);
            return result;

        }

        public static void OpenWindow(BaseWindow window)
        {
            windows.Add(window);
        }

        internal static void RaiseWindow(BaseWindow window)
        {
            windows.Remove(window);
            windows.Add(window);
        }

        public void Main(string[] raw_args)
        {
            Logger.Log(LogLevel.AppStart, "Run sdl2 interface");
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
            SDL.SetHint("SDL_WINDOWS_DPI_AWARENESS", "permonitorv2");

            if (SDL.Init(SdlInitFlags.Video | SdlInitFlags.Events) != 0)
            {
                throw new Exception("SDL initialization failed");
            }
            if (TTF.Init() != 0)
            {
                throw new Exception("SDL TTF initialization failed");
            }
            SDL.SetHint("SDL_RENDER_DRIVER", "direct3d12");

            SDL.GetDisplayDPI(0, out var ddpi, out var hdpi, out var vdpi);
            SDL.GetDisplayBounds(0, out var rect);
            double Scale = hdpi / 96.0;
            int W = (int)(rect.Width * 0.8);
            int H = (int)(rect.Height * 0.8);

            var window = SDL.CreateWindow("PoweEditor", SDL.WINDOWPOS_CENTERED, SDL.WINDOWPOS_CENTERED, W, H, WindowFlags.Shown | WindowFlags.Resizable);
            if (window.IsNull)
            {
                throw new Exception("SDL window initialization failed");
            }
            var renderer = SDL.CreateRenderer(window, -1, RendererFlags.Accelerated);
            if (renderer.IsNull)
            {
                throw new Exception("SDL render initialization failed");
            }

            Render render = new(new(renderer, new EditorFramework.ColorTheme()), renderer, window);

            /* create application instance */
            {
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

                // start widget

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
                        _ = Task.Run(() => project.OpenFile(file));
                    }
                    else
                    {
                        _ = Task.Run(() => project.CreateFile(null, "c"));
                    }
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
                    SDL.RenderPresent(render.renderer);
                    Thread.Sleep(10);
                    while (SDL.PollEvent(out Event evt) != 0)
                    {
                        if (evt.Type == EventType.WindowEvent)
                        {
                            W = evt.Window.Data1;
                            H = evt.Window.Data2;
                            Render.W = W; Render.H = H;
                        }
                        if (evt.Type == EventType.KeyDown && evt.Keyboard.Keysym.Scancode == Scancode.F11)
                        {
                            WindowFlags flags = SDL.GetWindowFlags(window);
                            if (flags.HasFlag(WindowFlags.Fullscreen))
                            {
                                SDL.SetWindowFullscreen(window, 0);
                            }
                            else
                            {
                                SDL.SetWindowFullscreen(window, WindowFlags.Fullscreen);
                            }
                        }
                        else
                        {
                            var e = CreateEvent(evt);
                            if (e != null)
                            {
                                pool.AddEvent(e);
                                pool.ProcessEvents();
                            }
                        }  
                    }
                }
            }

            SDL.DestroyRenderer(render.renderer);
            SDL.DestroyWindow(render.SDLWindow);
            SDL.Quit();
            TTF.Quit();
        }

        EditorFramework.Events.MouseButton Convert(SDL_Sharp.MouseButton button)
        {
            return button switch
            {
                SDL_Sharp.MouseButton.Left => EditorFramework.Events.MouseButton.Left,
                SDL_Sharp.MouseButton.Middle => EditorFramework.Events.MouseButton.Middle,
                SDL_Sharp.MouseButton.Right => EditorFramework.Events.MouseButton.Right,
                _ => 0
            };
        }

        EditorFramework.Events.MouseButton Convert(SDL_Sharp.MouseButtonMask state)
        {
            EditorFramework.Events.MouseButton result = 0;
            if ((state & SDL_Sharp.MouseButtonMask.Left) != 0) result |= EditorFramework.Events.MouseButton.Left;
            if ((state & SDL_Sharp.MouseButtonMask.Middle) != 0) result |= EditorFramework.Events.MouseButton.Middle;
            if ((state & SDL_Sharp.MouseButtonMask.Right) != 0) result |= EditorFramework.Events.MouseButton.Right;
            return result;
        }

        KeyMode Convert(SDL_Sharp.KeyModifier key)
        {
            KeyMode mode = KeyMode.None;
            if ((key & SDL_Sharp.KeyModifier.LeftShift) != 0) mode |= KeyMode.LeftShift;
            if ((key & SDL_Sharp.KeyModifier.RightShift) != 0) mode |= KeyMode.RightShift;
            if ((key & SDL_Sharp.KeyModifier.LeftCtrl) != 0) mode |= KeyMode.LeftCtrl;
            if ((key & SDL_Sharp.KeyModifier.RightCtrl) != 0) mode |= KeyMode.RightCtrl;
            if ((key & SDL_Sharp.KeyModifier.LeftAlt) != 0) mode |= KeyMode.LeftAlt;
            if ((key & SDL_Sharp.KeyModifier.RightAlt) != 0) mode |= KeyMode.RightAlt;
            if ((key & SDL_Sharp.KeyModifier.LeftGui) != 0) mode |= KeyMode.LeftWin;
            if ((key & SDL_Sharp.KeyModifier.RightGui) != 0) mode |= KeyMode.RightWin;
            if ((key & SDL_Sharp.KeyModifier.Caps) != 0) mode |= KeyMode.CapsLock;
            if ((key & SDL_Sharp.KeyModifier.Num) != 0) mode |= KeyMode.NumLock;
            return mode;
        }

        KeyCode Convert(SDL_Sharp.Scancode key)
        {
            return key switch
            {
                // Letters A-Z
                >= SDL_Sharp.Scancode.A and <= SDL_Sharp.Scancode.Z
                    => (KeyCode)((int)KeyCode.A + (key - SDL_Sharp.Scancode.A)),

                // Digits D1-D9 + D0
                >= SDL_Sharp.Scancode.D1 and <= SDL_Sharp.Scancode.D9
                    => (KeyCode)((int)KeyCode.D1 + (key - SDL_Sharp.Scancode.D1)),
                SDL_Sharp.Scancode.D0 => KeyCode.D0,

                >= SDL_Sharp.Scancode.F1 and <= SDL_Sharp.Scancode.F12
                    => (KeyCode)((int)KeyCode.F1 + (key - SDL_Sharp.Scancode.F1)),
                >= SDL_Sharp.Scancode.F13 and <= SDL_Sharp.Scancode.F24
                    => (KeyCode)((int)KeyCode.F13 + (key - SDL_Sharp.Scancode.F13)),

                // Numpad Digits
                >= SDL_Sharp.Scancode.KeyPad1 and <= SDL_Sharp.Scancode.KeyPad9
                    => (KeyCode)((int)KeyCode.NumPad1 + (key - SDL_Sharp.Scancode.KeyPad1)),
                SDL_Sharp.Scancode.KeyPad0 => KeyCode.NumPad0,

                // Numpad Ops
                SDL_Sharp.Scancode.KeyPadMultiply => KeyCode.NumPadMultiply,
                SDL_Sharp.Scancode.KeyPadPlus => KeyCode.NumPadAdd,
                SDL_Sharp.Scancode.KeyPadMinus => KeyCode.NumPadSubtract,
                SDL_Sharp.Scancode.KeyPadDecimal => KeyCode.NumPadDecimal,
                SDL_Sharp.Scancode.KeyPadDivide => KeyCode.NumPadDivide,
                SDL_Sharp.Scancode.KeyPadEnter => KeyCode.Enter,

                // nav
                SDL_Sharp.Scancode.Return => KeyCode.Enter,
                SDL_Sharp.Scancode.Escape => KeyCode.Escape,
                SDL_Sharp.Scancode.Backspace => KeyCode.Backspace,
                SDL_Sharp.Scancode.Tab => KeyCode.Tab,
                SDL_Sharp.Scancode.Space => KeyCode.Space,
                SDL_Sharp.Scancode.Pause => KeyCode.Pause,
                SDL_Sharp.Scancode.CapsLock => KeyCode.CapsLock,
                SDL_Sharp.Scancode.ScrollLock => KeyCode.ScrollLock,
                SDL_Sharp.Scancode.NumLockClear => KeyCode.NumLock,
                SDL_Sharp.Scancode.PrintScreen => KeyCode.PrintScreen,
                SDL_Sharp.Scancode.Insert => KeyCode.Insert,
                SDL_Sharp.Scancode.Delete => KeyCode.Delete,
                SDL_Sharp.Scancode.Home => KeyCode.Home,
                SDL_Sharp.Scancode.End => KeyCode.End,
                SDL_Sharp.Scancode.PageUp => KeyCode.PageUp,
                SDL_Sharp.Scancode.PageDown => KeyCode.PageDown,
                SDL_Sharp.Scancode.Help => KeyCode.Help,
                SDL_Sharp.Scancode.Right => KeyCode.Right,
                SDL_Sharp.Scancode.Left => KeyCode.Left,
                SDL_Sharp.Scancode.Down => KeyCode.Down,
                SDL_Sharp.Scancode.Up => KeyCode.Up,

                // mods
                SDL_Sharp.Scancode.LShift => KeyCode.LeftShift,
                SDL_Sharp.Scancode.RShift => KeyCode.RightShift,
                SDL_Sharp.Scancode.LCtrl => KeyCode.LeftControl,
                SDL_Sharp.Scancode.RCtrl => KeyCode.RightControl,
                SDL_Sharp.Scancode.LAlt => KeyCode.LeftAlt,
                SDL_Sharp.Scancode.RAlt => KeyCode.RightAlt,
                SDL_Sharp.Scancode.LGui => KeyCode.LeftWindows,
                SDL_Sharp.Scancode.RGui => KeyCode.RightWindows,
                SDL_Sharp.Scancode.Application => KeyCode.Applications,
                SDL_Sharp.Scancode.Sleep => KeyCode.Sleep,

                // symbols + oem
                SDL_Sharp.Scancode.Minus => KeyCode.Minus,
                SDL_Sharp.Scancode.Equals => KeyCode.Equal,
                SDL_Sharp.Scancode.LeftBracket => KeyCode.OpenBrackets,
                SDL_Sharp.Scancode.RightBracket => KeyCode.CloseBrackets,
                SDL_Sharp.Scancode.SemiColon => KeyCode.Semicolon,
                SDL_Sharp.Scancode.Apostrophe => KeyCode.Quotes,
                SDL_Sharp.Scancode.Grave => KeyCode.Tilde,
                SDL_Sharp.Scancode.Comma => KeyCode.Comma,
                SDL_Sharp.Scancode.Period => KeyCode.Period,
                SDL_Sharp.Scancode.Slash => KeyCode.OemQuestion,
                SDL_Sharp.Scancode.Backslash => KeyCode.Backslash,
                SDL_Sharp.Scancode.NonUSHash => KeyCode.Backslash,

                _ => 0
            };
        }

        private EventBase? CreateEvent(Event evt)
        {
            switch (evt.Type)
            {
                case EventType.Quit:
                    return new EditorFramework.Events.QuitEvent();
                case EventType.TextInput:
                    return new EditorFramework.Events.TextInputEvent(GetTextInputBytes(evt.Text));
                case EventType.KeyDown:
                    return new KeyDownEvent(Convert(evt.Keyboard.Keysym.Scancode), Convert(evt.Keyboard.Keysym.Mod));
                case EventType.KeyUp:
                    return new KeyUpEvent(Convert(evt.Keyboard.Keysym.Scancode), Convert(evt.Keyboard.Keysym.Mod));
                case EventType.MouseButtonDown:
                    return new MouseDownEvent(evt.Button.X, evt.Button.Y, Convert(SDL.GetMouseState(out _, out _)), Convert(evt.Button.Button));
                case EventType.MouseButtonUp:
                    return new MouseUpEvent(evt.Button.X, evt.Button.Y, Convert(SDL.GetMouseState(out _, out _)), Convert(evt.Button.Button));
                case EventType.MouseMotion:
                    return new MouseMoveEvent(evt.Motion.X, evt.Motion.Y, Convert(evt.Motion.State), evt.Motion.XRel, evt.Motion.YRel);
                case EventType.MouseWheel:
                    var state = SDL.GetMouseState(out var x, out var y);
                    return new EditorFramework.Events.MouseWheelEvent(x, y, Convert(state), evt.Wheel.X, evt.Wheel.Y);
            }
            return null;
        }

        public void RemoveWindow(BaseWindow window)
        {
            windows.Remove(window);
        }

        public IEnumerable<BaseWindow> ListWindows()
        {
            return windows;
        }

        public void SetClipboard(string text)
        {
            if (SDL.SetClipboardText(text) != 0)
            {
                string error = SDL.GetError();
                Logger.Log(LogLevel.Error, $"Error when copying to buffer: {error}");
            }
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
