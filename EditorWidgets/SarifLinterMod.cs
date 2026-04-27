using Common;
using EditorCore.File;
using EditorCore.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EditorFramework
{
    public class SarifLinterMod
    {
        struct SimpleErrorMark : IErrorMark
        {
            public SimpleErrorMark(string message, long begin, long end, ErrorMarkSeverity severity, string source)
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
            public string Source { get; init; }
        }

        public static void Init(EditorServer server)
        {
            server.ActionOnFileSave += OnFileSave;
            server.ActionOnFileOpen += OnFileSave;
        }

        public static void OnFileSave(EditorFile file)
        {
            if (!File.Exists(file.filename)) { return; }
            _ = Task.Run(async () =>
            {
                Logger.Log("File saved [sarif linter]");
                if (file.Buffer.Text.Length > 1024 * 1024)
                {
                    lock (file.Buffer.ErrorMarksLock)
                    {
                        file.Buffer.ErrorMarks.Clear();
                    }
                    Logger.Log(LogLevel.Warning, "Too big file, disable sarif linter");
                    return;
                }
                lock (file.Buffer.ErrorMarksLock)
                {
                    file.Buffer.ErrorMarks.Clear();
                }
                string? language = file.LanguageId();

                (string executable, string args)[] LinterVariants = language switch
                {
                    "c" => [("clang", "-std=gnu2x -fsyntax-only -Weverything -fdiagnostics-format=sarif -fno-color-diagnostics -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE -fms-extensions -Wno-microsoft %f"),
                            ("gcc", "-fsyntax-only -Wall -Wextra -fdiagnostics-format=sarif -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE -fms-extensions -Wno-microsoft %f")],
                    "cpp" => [("clang++", "-std=gnu++2c -fsyntax-only -fdiagnostics-format=sarif -fno-color-diagnostics -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE -fms-extensions -Wno-microsoft %f"),
                              ("g++", "-fsyntax-only -fdiagnostics-format=sarif -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE -fms-extensions -Wno-microsoft %f")],
                    "python" => [("ruff", "check --format sarif --quiet %f"),
                                 ("pylint", "--output-format=sarif %f")],
                    "javascript" or "typescript" => [("eslint", "-f sarif %f")],
                    "go" => [("staticcheck", "-f sarif ./...")],
                    "shellscript" => [("shellcheck", "-f sarif %f")],
                    // todo
                    // "csharp" => [("dotnet", "build /consoleloggerparameters:NoSummary /p:ErrorLog=report.sarif")],
                    "dockerfile" => [("hadolint", "-f sarif %f")],
                    "sql" => [("sqlfluff", "lint --format sarif %f")],

                    _ => []
                };

                Logger.Log($"Language id: {language}");

                foreach (var Linter in LinterVariants)
                {
                    if (file.filename == null) continue;

                    Process? process = null;

                    try
                    {
                        Logger.Log($"Running {Linter.executable} for {file.filename}");

                        ProcessStartInfo startInfo = new()
                        {
                            FileName = Linter.executable,
                            Arguments = Linter.args.Replace("%f", $"\"{file.filename}\""),
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8
                        };

                        process = new Process { StartInfo = startInfo };

                        process.Start();

                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        var output = process.StandardOutput.ReadToEndAsync(cts.Token);
                        var error = process.StandardError.ReadToEndAsync(cts.Token);
                        await process.WaitForExitAsync(cts.Token);
                        string fullOutput = (await output) + (await error);

                        string raw = fullOutput.ToString().Trim();
                        if (string.IsNullOrWhiteSpace(raw)) continue;

                        int startIdx = raw.IndexOf('{');
                        int endIdx = raw.LastIndexOf('}');
                        if (startIdx == -1 || endIdx == -1 || endIdx < startIdx) continue;

                        string json = raw.Substring(startIdx, endIdx - startIdx + 1);

                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            if (!doc.RootElement.TryGetProperty("runs", out JsonElement runs)) return;

                            foreach (JsonElement run in runs.EnumerateArray())
                            {
                                if (!run.TryGetProperty("results", out JsonElement results)) continue;

                                foreach (JsonElement result in results.EnumerateArray())
                                {
                                    string msg = "Unknown error";
                                    if (result.TryGetProperty("message", out JsonElement msgProp))
                                        msg = msgProp.GetProperty("text").GetString() ?? msg;

                                    ErrorMarkSeverity severity = ErrorMarkSeverity.Error;
                                    if (result.TryGetProperty("level", out JsonElement levelProp))
                                    {
                                        string level = levelProp.GetString()?.ToLower() ?? "";
                                        if (level == "warning" || level == "note") severity = ErrorMarkSeverity.Warning;
                                    }

                                    if (!result.TryGetProperty("locations", out JsonElement locations) || locations.GetArrayLength() == 0) continue;

                                    JsonElement firstLoc = locations[0];
                                    if (!firstLoc.TryGetProperty("physicalLocation", out JsonElement physLoc)) continue;

                                    string? errorFilePath = null;
                                    if (physLoc.TryGetProperty("artifactLocation", out JsonElement artLoc) && artLoc.TryGetProperty("uri", out JsonElement uriProp))
                                    {
                                        try { errorFilePath = new Uri(uriProp.GetString()!).LocalPath; } catch { continue; }
                                    }
                                    if (string.IsNullOrEmpty(errorFilePath)) continue;
                                    string normError = Path.GetFullPath(errorFilePath).TrimEnd('\\', '/');

                                    if (!physLoc.TryGetProperty("region", out JsonElement region)) continue;

                                    int sLine = region.GetProperty("startLine").GetInt32() - 1;
                                    int sCol = region.GetProperty("startColumn").GetInt32() - 1;

                                    int eLine = (region.TryGetProperty("endLine", out JsonElement el) ? el.GetInt32() : sLine + 1) - 1;
                                    int eCol = (region.TryGetProperty("endColumn", out JsonElement ec) ? ec.GetInt32() : sCol + 1) - 1;

                                    lock (file.Buffer.Server.FilesLock)
                                    {
                                        var targetFile = file.Buffer.Server.Files.FirstOrDefault(x =>
                                            x.filename != null &&
                                            string.Equals(Path.GetFullPath(x.filename).TrimEnd('\\', '/'), normError, StringComparison.OrdinalIgnoreCase)
                                        );

                                        if (targetFile != null)
                                        {
                                            long posStart = targetFile.Buffer.GetPosition(sLine, sCol);
                                            long posEnd = targetFile.Buffer.GetPosition(eLine, eCol);

                                            if (posEnd - posStart <= 1)
                                            {
                                                posEnd = Math.Min(posStart + 2, targetFile.Buffer.Text.Length);
                                            }

                                            lock (targetFile.Buffer.ErrorMarksLock)
                                            {
                                                targetFile.Buffer.ErrorMarks.Add(new SimpleErrorMark(
                                                    msg,
                                                    Math.Max(posStart, 0),
                                                    posEnd,
                                                    severity,
                                                    $"::sarif-linter-mod::{file.filename}"
                                                ));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        Logger.Log($"Linter {Linter.executable} finished, found errors.");
                    }
                    catch (OperationCanceledException) { process?.Kill(); Logger.Log(LogLevel.Error, "Linter timeout."); }
                    catch (Exception ex) { process?.Kill(); Logger.Log(LogLevel.Error, $"Linter error: {ex.Message}"); }
                }
            });
        }
    }
}