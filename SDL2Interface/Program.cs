using CommandProviderInterface;
using PowershellCommandProvider;
using EditorCore.File;
using EditorCore.Selection;
using EditorCore.Server;
using SDL_Sharp;
using SDL_Sharp.Ttf;
using System.Runtime.InteropServices;
using System.Text;
using PythonCommandProvider;
using EditorCore.Buffer;
using RegexTokenizer;

namespace SDL2Interface
{
    internal class Program
    {
        public static List<BaseWindow> windows = [];

        public static void OpenWindow(BaseWindow window)
        {
            windows.Add(window);
        }

        static void Main(string[] args)
        {
            string? fileToOpen = args.ElementAtOrDefault(1);
            Console.WriteLine($"Opening \"{fileToOpen}\"...");
            if (SDL.Init(SdlInitFlags.Everything) != 0)
            {
                throw new Exception("SDL initialization failed");
            }
            if (TTF.Init() != 0)
            {
                throw new Exception("SDL TTF initialization failed");
            }
            if (SDL.CreateWindowAndRenderer(1600, 900, 0, out BaseWindow.window, out BaseWindow.renderer) != 0)
            {
                throw new Exception("SDL initialization failed");
            }
            SDL.GetWindowSize(BaseWindow.window, out BaseWindow.W, out BaseWindow.H);

            /* create application instance */

            {
                PowershellProvider provider = new();
                //PythonProvider provider = new();
                EditorServer server = new(provider);
                if (fileToOpen != null)
                {
                    EditorFile file = new(server, fileToOpen);
                    windows.Add(new FileEditorWindow(file, new Rect(0, 0, BaseWindow.W, BaseWindow.H)));
                }
                else
                {
                    windows.Add(new InputTextWindow(new EditorBuffer(server, BaseTokenizer.CreateTokenizer("")), new Rect(0, 0, BaseWindow.W, BaseWindow.H)));
                }
            }

            while (windows.Count > 0)
            {
                foreach (var win in windows)
                {
                    win.Draw();
                }
                SDL.RenderPresent(BaseWindow.renderer);
                Thread.Sleep(10);
                while (SDL.PollEvent(out Event evt) != 0)
                {
                    foreach (var win in windows.Reverse<BaseWindow>())
                    {
                        if (!win.HandleEvent(evt))
                        {
                            break;
                        }
                    }
                }
            }

            SDL.DestroyRenderer(BaseWindow.renderer);
            SDL.DestroyWindow(BaseWindow.window);
            SDL.Quit();
            TTF.Quit();
        }
    }
}
