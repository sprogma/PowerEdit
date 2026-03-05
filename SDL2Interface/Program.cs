using CommandProviderInterface;
using EditorCore.Buffer;
using EditorCore.File;
using EditorCore.Selection;
using EditorCore.Server;
using Lsp;
using PowershellCommandProvider;
using PythonCommandProvider;
using RegexTokenizer;
using SDL_Sharp;
using SDL_Sharp.Ttf;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using TextBuffer;

namespace SDL2Interface
{
    internal class Program
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

        static void Main(string[] raw_args)
        {
            List<string> args = [..raw_args];

            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            List<string?> fileToOpen = [.. args.Where(x => !x.StartsWith("-")).Cast<string?>()];
            if (fileToOpen.Count == 0)
            {
                fileToOpen.Add(null);
            }
            //fileToOpen = ["D:\\big.txt"];
            fileToOpen = ["C:\\Users\\User\\AppData\\Local\\Temp\\copy_10gb.txt"];
            //fileToOpen = ["C:\\Users\\User\\AppData\\Local\\Temp\\big.txt"]; // too big for now
            Console.WriteLine($"Opening files {fileToOpen.Count} \"{fileToOpen}\"...");
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
                    Console.WriteLine("Error: Not selected any provider");
                    return;
                }

                FileTabsWindow tabs = new([], new());

                ProjectEditorWindow project = new(
                    tabs.OpenFile,
                    tabs.RaiseFile,
                    tabs,
                    provider,
                    new Rect(0, 0, BaseWindow.W, BaseWindow.H)
                );
                EditorServer server = new(provider);

                // add "lsp" module
                SimpleLinterMod.Init(server);

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
                        if (!win.Event(evt))
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
