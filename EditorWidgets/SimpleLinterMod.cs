using EditorCore.Buffer;
using EditorCore.File;
using EditorCore.Server;
using Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace EditorFramework
{
    public class SimpleLinterMod
    {
        struct SimpleErrorMark : IErrorMark
        {
            public SimpleErrorMark(string message, long begin, long end, ErrorMarkSeverity severity, string? source)
            {
                Message = message;
                Begin = begin;
                End = end;
                Severity = severity;
                Source = source;
            }

            public string Message { get; init; }
            public long Begin { get; set; }
            public long End { get; set; }
            public ErrorMarkSeverity Severity { get; init; }
            public string? Source { get; init; }
        }

        public static void Init(EditorServer server)
        {
            server.ActionOnFileSave += OnFileSave;
        }

        public static void OnFileSave(EditorFile file)
        {
            _ = Task.Run(async () =>
            {
                Logger.Log("File saved [simple linter]");
                if (file.Buffer.Text.Length > 1024 * 1024)
                {
                    lock (file.Buffer.ErrorMarksLock)
                    {
                        file.Buffer.ErrorMarks.Clear();
                    }
                    Logger.Log(LogLevel.Warning, "Too big file, disable linter");
                    return;
                }
                lock (file.Buffer.ErrorMarksLock)
                {
                    file.Buffer.ErrorMarks.Clear();
                }
                string? language = file.LanguageId();
                (string executable, string args, string pattern, string? temporary)[] LinterVariants = language switch
                {
                    "hive" => [("D:/mipt/lang3/a.exe", "--no-output=true --input-file=%f", @"Error:.near.%f:%l:%c>%m", null)],
                    "c" => [("clang", "-std=gnu2x -fsyntax-only -ferror-limit=5000 -Wall -Wextra -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE -fms-extensions -Wno-microsoft %f", @"%f:%l:%c:.+: %m", null),
                            ("gcc", "-fsyntax-only -Wall -Wextra %f", @"%f:%l:%c:.+: %m", null)],
                    "cpp" => [("clang", "-std=gnu++2c -fsyntax-only -ferror-limit=5000 -Wall -Wextra -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE -fms-extensions -Wno-microsoft %f", @"%f:%l:%c:.+: %m", null),
                              ("g++", "-fsyntax-only -Wall -Wextra %f", @"%f:%l:%c:.+: %m", null)],
                    "d" => [("dmd", "-color=off -o- -w -wi -c %f", @"%f\(%l\):.+: %m", null),
                                ("ldc2", "--o- --vcolumns -w -c %f", @"%f\(%l,%c\):[^:]+: %m", null),
                                ("gdc", "-fsyntax-only -Wall -Wextra %f", @"%f:%l:%c:.+: %m", null)],
                    "go" => [("go", "build -o devnull %d", @"%f:%l:%c:? %m", null),
                                    ("go", "vet", @"%f:%l:%c: %m", null)],
                    // TODO "haskell" => [("hlint", "%f", @"%f:(?%l[,:]%c)?.-: %m")],
                    "java" => [("javac", "-d %d %f", @"%f:%l: error: %m", null)],
                    "javascript" => [("eslint", "-f compact %f", @"%f: line %l, col %c, %m", null),
                                            ("jshint", "%f", @"%f: line %l,.+, %m", null)],
                    "literate" => [("lit", "-c %f", @"%f:%l:%m", null)],
                    "lua" => [("luacheck", "--no-color %f", @"%f:%l:%c: %m", null)],
                    "nim" => [("nim", "check --listFullPaths --stdout --hints:off %f", @"%f.%l, %c. %m", null)],
                    "nix" => [("nix-linter", "%f", @"%m at %f:%l:%c", null)],
                    "objective-c" => [("xcrun", "clang -fsyntax-only -Wall -Wextra %f", @"%f:%l:%c:.+: %m", null)],
                    "python" => [("pyflakes", "%f", @"%f:%l:.-:? %m", null),
                                        ("mypy", "%f", @"%f:%l: %m", null),
                                        ("pylint", "--output-format=parseable --reports=no %f", @"%f:%l: %m", null),
                                        ("ruff", "check --output-format=concise %f", @"%f:%l:%c: %m", null),
                                        ("flake8", "%f", @"%f:%l:%c: %m", null)],
                    "rust" => [("cargo", "clippy --message-format short", @"%f:%l:%c: %m", null)],
                    "shellscript" => [("shfmt", "%f", @"%f:%l:%c: %m", null),
                                    ("shellcheck", "-f gcc %f", @"%f:%l:%c:.+: %m", null)],
                    "swift" => [("xcrun", "swiftc %f", @"%f:%l:%c:.+: %m", null),
                                    ("swiftc", "%f", @"%f:%l:%c:.+: %m", null)],
                    "yaml" => [("yamllint", "--format parsable %f", @"%f:%l:%c:.+ %m", null)],
                    // todo
                    //"csharp" => [("dotnet", $"""exec "C:\Program Files\dotnet\sdk\10.0.103\Roslyn\bincore\csc.dll" -noconfig -target:library "-out:{Path.GetTempPath()}\rnd.dll" -utf8output -warnaserror- "%f" """, @"%f\(%l,%c\): %m", $"{Path.GetTempPath()}\\rnd.dll")],
                    "typescript" => [("tsc", "--noEmit --pretty false %f", @"%f\(%l,%c\): error %m", null)],
                    "html" => [("tidy", "-e -q %f", @"line %l column %c - %m", null)],
                    "css" => [("stylelint", "--formatter compact %f", @"%f: line %l, col %c, %m", null)],
                    "json" => [("jsonlint", "-q %f", @"%f: line %l, col %c, %m", null)],
                    "sql" => [("sqlfluff", "lint --format parsable %f", @"%f:%l:%c: %m", null)],
                    "dockerfile" => [("hadolint", "--no-color %f", @"%f:%l %m", null)],
                    "markdown" => [("markdownlint", "--style default %f", @"%f:%l %m", null)],
                    "ruby" => [("rubocop", "--format emacs %f", @"%f:%l:%c: .+: %m", null)],
                    "php" => [("php", "-l %f", @"Parse error: %m in %f on line %l", null)],
                    _ => []
                };

                Logger.Log($"Language id: {language}");


                foreach (var Linter in LinterVariants)
                {
                    if (file.filename == null) { return; }
                    try
                    {
                        Logger.Log($"Using {Linter} at file {file.filename}");
                        ProcessStartInfo startInfo = new()
                        {
                            FileName = Linter.executable,
                            Arguments = Linter.args.Replace("%f", file.filename),
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8,
                        };
                        if (Linter.temporary != null)
                        {
                            File.Delete(Linter.temporary);
                        }

                        using (Process process = new Process { StartInfo = startInfo })
                        {
                            string pattern = Linter.pattern.Replace("%f", $@"(?<file>.*?{Regex.Escape(file.filename ?? "")})")
                                                            .Replace("%l", @"(?<line>\d+)")
                                                            .Replace("%c", @"(?<col>\d+)")
                                                            .Replace("%m", @"(?<msg>.+)");
                            Regex patternRegex = new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

                            void UpdateError(string filename, int line, int col, string msg)
                            {
                                long position = file.Buffer.GetPosition(line, col);
                                lock (file.Buffer.ErrorMarksLock)
                                {
                                    file.Buffer.ErrorMarks.Add(new SimpleErrorMark(msg, Math.Max(position - 1, 0), Math.Min(position + 2, file.Buffer.Text.Length), ErrorMarkSeverity.Error, null));
                                }
                            }

                            process.OutputDataReceived += (sender, e) =>
                            {
                                if (e.Data != null)
                                {
                                    Match match = patternRegex.Match(e.Data);
                                    if (match.Success)
                                    {
                                        string filename = match.Groups["file"].Value;
                                        int line = match.Groups.ContainsKey("line") ? int.Parse(match.Groups["line"].Value) : 1;
                                        int col = match.Groups.ContainsKey("col") ? int.Parse(match.Groups["col"].Value) : 1;
                                        string msg = match.Groups["msg"].Value;
                                        UpdateError(filename, line - 1, col - 1, msg);
                                    }
                                    //Logger.Log($"{e.Data}");
                                }
                            };

                            process.ErrorDataReceived += (sender, e) =>
                            {
                                if (e.Data != null)
                                {
                                    Match match = patternRegex.Match(e.Data);
                                    if (match.Success)
                                    {
                                        string filename = match.Groups["file"].Value;
                                        int line = match.Groups.ContainsKey("line") ? int.Parse(match.Groups["line"].Value) : 1;
                                        int col = match.Groups.ContainsKey("col") ? int.Parse(match.Groups["col"].Value) : 1;
                                        string msg = match.Groups["msg"].Value;
                                        UpdateError(filename, line - 1, col - 1, msg);
                                    }
                                    //Logger.Log(LogLevel.Error, $"{e.Data}");
                                }
                            };

                            process.Start();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            await process.WaitForExitAsync();

                            Logger.Log("Process Completed.");
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        /* linter is unaviable */
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Error, $"At processing: {ex.Message}");
                        return;
                    }
                }
            });
        }
    }
}
