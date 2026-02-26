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

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            string? fileToOpen = args.Where(x => !x.StartsWith("-")).ElementAtOrDefault(0);
            //fileToOpen = "D:\\mipt\\a.c";
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
                ICommandProvider? provider;

                if (args.Contains("--python"))
                {
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
                EditorServer server = new(provider);



                // add "lsp" module
                server.ActionOnFileSave += (EditorFile f) =>
                {
                    Task.Run(async () =>
                    {
                        Console.WriteLine("File saved!");
                        if (f.Buffer.Text.Length > 1024 * 1024)
                        {
                            f.Buffer.ErrorMarks.Clear();
                            Console.WriteLine("-- Too big file, disable linter");
                            return; 
                        }
                        f.Buffer.ErrorMarks.Clear();
                        string language = Path.GetExtension(f.filename)?[1..]?.ToLower() switch
                        {
                            "cpp" or "cxx" or "cc" or "c++" or "hpp" or "hxx" or "hh" => "c++",
                            "c" or "h" => "c",
                            "d" or "di" or "dd" => "d",
                            "go" => "go",
                            "hs" or "lhs" => "haskell",
                            "java" or "class" or "jar" => "java",
                            "js" or "mjs" or "cjs" or "jsx" => "javascript",
                            "lit" or "lp" => "literate",
                            "lua" => "lua",
                            "nim" or "nims" or "nimble" => "nim",
                            "nix" => "nix",
                            "m" or "mm" or "M" => "objective-c",
                            "py" or "pyw" or "pyi" => "python",
                            "rs" => "rust",
                            "sh" or "bash" or "zsh" or "ksh" => "shell",
                            "swift" => "swift",
                            "yaml" or "yml" => "yaml",
                            _ => "undefined"
                        };
                        (string executable, string args, string pattern)[] LinterVariants = language switch
                        {
                            "c" => [("gcc", "-fsyntax-only -Wall -Wextra %f", @"%f:%l:%c:.+: %m")],
                            "c++" => [("g++", "-fsyntax-only -Wall -Wextra %f", @"%f:%l:%c:.+: %m")],
                            "d" => [("dmd", "-color=off -o- -w -wi -c %f", @"%f\(%l\):.+: %m"),
                                    ("ldc2", "--o- --vcolumns -w -c %f", @"%f\(%l,%c\):[^:]+: %m"),
                                    ("gdc", "-fsyntax-only -Wall -Wextra %f", @"%f:%l:%c:.+: %m")],
                            "go" => [("go", "build -o devnull %d", @"%f:%l:%c:? %m"),
                                     ("go", "vet", @"%f:%l:%c: %m")],
                            // TODO "haskell" => [("hlint", "%f", @"%f:(?%l[,:]%c)?.-: %m")],
                            "java" => [("javac", "-d %d %f", @"%f:%l: error: %m")],
                            "javascript" => [("eslint", "-f compact %f", @"%f: line %l, col %c, %m"),
                                             ("jshint", "%f", @"%f: line %l,.+, %m")],
                            "literate" => [("lit", "-c %f", @"%f:%l:%m")],
                            "lua" => [("luacheck", "--no-color %f", @"%f:%l:%c: %m")],
                            "nim" => [("nim", "check --listFullPaths --stdout --hints:off %f", @"%f.%l, %c. %m")],
                            "nix" => [("nix-linter", "%f", @"%m at %f:%l:%c")],
                            "objective-c" => [("xcrun", "clang -fsyntax-only -Wall -Wextra %f", @"%f:%l:%c:.+: %m")],
                            "python" => [("pyflakes", "%f", @"%f:%l:.-:? %m"),
                                         ("mypy", "%f", @"%f:%l: %m"),
                                         ("pylint", "--output-format=parseable --reports=no %f", @"%f:%l: %m"),
                                         ("ruff", "check --output-format=concise %f", @"%f:%l:%c: %m"),
                                         ("flake8", "%f", @"%f:%l:%c: %m")],
                            "rust" => [("cargo", "clippy --message-format short", @"%f:%l:%c: %m")],
                            "shell" => [("shfmt", "%f", @"%f:%l:%c: %m"),
                                        ("shellcheck", "-f gcc %f", @"%f:%l:%c:.+: %m")],
                            "swift" => [("xcrun", "swiftc %f", @"%f:%l:%c:.+: %m"),
                                        ("swiftc", "%f", @"%f:%l:%c:.+: %m")],
                            "yaml" => [("yamllint", "--format parsable %f", @"%f:%l:%c:.+ %m")],
                            _ => []
                        };

                        Console.WriteLine($"Language id: {language}");

                        foreach (var Linter in LinterVariants)
                        {
                            try
                            {
                                Console.WriteLine(Linter);
                                ProcessStartInfo startInfo = new ProcessStartInfo
                                {
                                    FileName = Linter.executable,
                                    Arguments = Linter.args.Replace("%f", f.filename),
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                };

                                using (Process process = new Process { StartInfo = startInfo })
                                {
                                    string pattern = Linter.pattern.Replace("%f", $@"(?<file>.*?{f.filename})")
                                                                   .Replace("%l",  @"(?<line>\d+)")
                                                                   .Replace("%c",  @"(?<col>\d+)")
                                                                   .Replace("%m",  @"(?<msg>.+)");
                                    Regex patternRegex = new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

                                    void UpdateError(string file, int line, int col, string msg)
                                    {
                                        f.Buffer.ErrorMarks.Add(new(msg, f.Buffer.GetPosition(line, col)));
                                    }

                                    process.OutputDataReceived += (sender, e) =>
                                    {
                                        if (e.Data != null)
                                        {
                                            Match match = patternRegex.Match(e.Data);
                                            if (match.Success)
                                            {
                                                string file = match.Groups["file"].Value;
                                                int line = match.Groups.ContainsKey("line") ? int.Parse(match.Groups["line"].Value) : 1;
                                                int col = match.Groups.ContainsKey("col") ? int.Parse(match.Groups["col"].Value) : 1;
                                                string msg = match.Groups["msg"].Value;
                                                UpdateError(file, line, col - 1, msg);
                                            }
                                            Console.WriteLine($"[OUTPUT]: {e.Data}");
                                        }
                                    };

                                    process.ErrorDataReceived += (sender, e) =>
                                    {
                                        if (e.Data != null)
                                        {
                                            Match match = patternRegex.Match(e.Data);
                                            if (match.Success)
                                            {
                                                string file = match.Groups["file"].Value;
                                                int line = match.Groups.ContainsKey("line") ? int.Parse(match.Groups["line"].Value) : 1;
                                                int col = match.Groups.ContainsKey("col") ? int.Parse(match.Groups["col"].Value) : 1;
                                                string msg = match.Groups["msg"].Value;
                                                UpdateError(file, line, col - 1, msg);
                                            }
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine($"[ERROR]: {e.Data}");
                                            Console.ResetColor();
                                        }
                                    };

                                    process.Start();
                                    process.BeginOutputReadLine();
                                    process.BeginErrorReadLine();

                                    await process.WaitForExitAsync();

                                    Console.WriteLine("Process Completed.");
                                }
                            }
                            catch (System.ComponentModel.Win32Exception)
                            {
                                /* linter is unaviable */
                                continue;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Processing Error: {ex.Message}");
                                return;
                            }
                        }
                    });
                };



                if (fileToOpen != null)
                {
                    EditorFile file = new(server, fileToOpen, new PersistentCTextBuffer());
                    windows.Add(new FileEditorWindow(file, new Rect(0, 0, BaseWindow.W, BaseWindow.H)));
                }
                else
                {
                    windows.Add(new InputTextWindow(new EditorBuffer(server, BaseTokenizer.CreateTokenizer("c"), null, "", new PersistentCTextBuffer()), new Rect(0, 0, BaseWindow.W, BaseWindow.H)));
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
