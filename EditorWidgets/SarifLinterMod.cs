using Common;
using EditorCore.Buffer;
using EditorCore.File;
using EditorCore.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TextBuffer;

namespace EditorFramework
{
    record struct FixItDataItem(long Begin, long End, string Value);

    public class SarifLinterMod
    {
        struct SarifErrorMark : IErrorMark
        {
            public SarifErrorMark(nint currentState, string message, long begin, long end, ErrorMarkSeverity severity, string source, FixItDataItem[] fixItData)
            {
                Message = message;
                Begin = begin;
                End = end;
                Severity = severity;
                Source = source;
                FixItData = fixItData;
                AnalysisState = currentState;
            }

            public string Message { get; init; }
            public long Begin { get; set; }
            public long End { get; set; }
            public ErrorMarkSeverity Severity { get; init; }
            public string Source { get; init; }


            private nint AnalysisState;

            private int FixApplyed = 0;

            private readonly FixItDataItem[] FixItData;

            public bool UpdateAfterDelete(long position, long count)
            {
                if (Begin >= position + count)
                {
                    Begin -= count;
                }
                else if (Begin >= position)
                {
                    Begin = position;
                }
                if (End >= position + count)
                {
                    End -= count;
                }
                else if (End >= position)
                {
                    End = position;
                }
                foreach (ref var fix in FixItData.AsSpan())
                {
                    if (fix.Begin >= position + count)
                    {
                        fix.Begin -= count;
                    }
                    else if (fix.Begin >= position)
                    {
                        fix.Begin = position;
                    }
                    if (fix.End >= position + count)
                    {
                        fix.End -= count;
                    }
                    else if (fix.End >= position)
                    {
                        fix.End = position;
                    }
                }
                return Begin < End;
            }

            public bool UpdateAfterInsert(long position, long count)
            {
                if (Begin >= position)
                {
                    Begin += count;
                }
                if (End >= position)
                {
                    End += count;
                }
                foreach (ref var fix in FixItData.AsSpan())
                {
                    if (fix.Begin >= position)
                    {
                        fix.Begin += count;
                    }
                    if (fix.End >= position)
                    {
                        fix.End += count;
                    }
                }
                return Begin < End;
            }

            public bool IsFixItAvailable(IEditorBuffer buffer)
            {
                return FixItData.Length != 0 && FixApplyed == 0;
            }

            public bool FixIt(IEditorBuffer buffer)
            {
                if (!IsFixItAvailable(buffer))
                {
                    return false;
                }

                if (Interlocked.Exchange(ref FixApplyed, 1) != 0)
                {
                    return false;
                }

                // apply fixits
                if (buffer.Text is IEditableTextBuffer etb)
                {
                    buffer.Fork();
                    foreach (var fix in FixItData.OrderByDescending(x => x.Begin))
                    {
                        buffer.DeleteString(fix.Begin, fix.End - fix.Begin);
                        buffer.InsertString(fix.Begin, fix.Value);
                    }
                    buffer.Commit();
                    Logger.Log("Sarif fix applyed");
                }

                return true;
            }
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
                var currentState = file.Buffer.Text.CurrentState;
                string? language = file.LanguageId();

                (string executable, string args)[] LinterVariants = language switch
                {
                    "c" => [("clang", "-std=gnu2x -fsyntax-only -Wall -Wextra -fdiagnostics-format=sarif -fno-color-diagnostics -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE -fms-extensions -Wno-microsoft %f"),
                            ("gcc", "-fsyntax-only -Wall -Wextra -fdiagnostics-format=sarif -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE -fms-extensions -Wno-microsoft %f")],
                    "cpp" => [("clang++", "-std=gnu++2c -fsyntax-only -fdiagnostics-format=sarif -fno-color-diagnostics -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE -fms-extensions -Wno-microsoft %f"),
                              ("g++", "-fsyntax-only -fdiagnostics-format=sarif -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE -fms-extensions -Wno-microsoft %f")],
                    "python" => [("ruff", "check %f --select E,F,UP,B,SIM,I --ignore D,ANN,COM --output-format sarif"),
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
                                    string? normError = Path.TryGetFullPath(errorFilePath)?.TrimEnd('\\', '/');

                                    if (!physLoc.TryGetProperty("region", out JsonElement region)) continue;

                                    int sLine = region.GetProperty("startLine").GetInt32() - 1;
                                    int sCol = region.GetProperty("startColumn").GetInt32() - 1;

                                    int eLine = (region.TryGetProperty("endLine", out JsonElement el) ? el.GetInt32() : sLine + 1) - 1;
                                    int eCol = (region.TryGetProperty("endColumn", out JsonElement ec) ? ec.GetInt32() : sCol + 1) - 1;

                                    lock (file.Buffer.Server.FilesLock)
                                    {
                                        var targetFile = file.Buffer.Server.Files.FirstOrDefault(x =>
                                            x.filename != null &&
                                            string.Equals(Path.TryGetFullPath(x.filename)?.TrimEnd('\\', '/'), normError, StringComparison.OrdinalIgnoreCase)
                                        );

                                        if (targetFile != null)
                                        {
                                            long posStart = targetFile.Buffer.GetPosition(sLine, sCol);
                                            long posEnd = targetFile.Buffer.GetPosition(eLine, eCol);

                                            if (posEnd - posStart <= 1)
                                            {
                                                posEnd = Math.Min(posStart + 2, targetFile.Buffer.Text.Length);
                                            }

                                            List<FixItDataItem> fixIts = new();
                                            if (result.TryGetProperty("fixes", out JsonElement fixesProp))
                                            {
                                                foreach (JsonElement fix in fixesProp.EnumerateArray())
                                                {
                                                    if (!fix.TryGetProperty("artifactChanges", out JsonElement changes)) continue;
                                                    foreach (JsonElement change in changes.EnumerateArray())
                                                    {
                                                        if (change.TryGetProperty("artifactLocation", out JsonElement chLoc) &&
                                                            chLoc.TryGetProperty("uri", out JsonElement chUri))
                                                        {
                                                            string cPath = new Uri(chUri.GetString()!).LocalPath;
                                                            if (!string.Equals(Path.TryGetFullPath(cPath)?.TrimEnd('\\', '/'), normError, StringComparison.OrdinalIgnoreCase))
                                                                continue;
                                                        }

                                                        if (change.TryGetProperty("replacements", out JsonElement replacements))
                                                        {
                                                            foreach (JsonElement rep in replacements.EnumerateArray())
                                                            {
                                                                if (rep.TryGetProperty("deletedRegion", out JsonElement delRegion))
                                                                {
                                                                    int rsL = delRegion.GetProperty("startLine").GetInt32() - 1;
                                                                    int rsC = (delRegion.TryGetProperty("startColumn", out var cS) ? cS.GetInt32() : 1) - 1;

                                                                    int reL = (delRegion.TryGetProperty("endLine", out var cEL) ? cEL.GetInt32() : rsL + 1) - 1;
                                                                    int reC = (delRegion.TryGetProperty("endColumn", out var cEC) ? cEC.GetInt32() : rsC + 1) - 1;

                                                                    string text = "";
                                                                    if (rep.TryGetProperty("insertedContent", out JsonElement content))
                                                                        text = content.GetProperty("text").GetString() ?? "";

                                                                    fixIts.Add(new FixItDataItem(
                                                                        targetFile.Buffer.GetPosition(rsL, rsC),
                                                                        targetFile.Buffer.GetPosition(reL, reC),
                                                                        text
                                                                    ));
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }


                                            lock (targetFile.Buffer.ErrorMarksLock)
                                            {
                                                targetFile.Buffer.ErrorMarks.Add(new SarifErrorMark(
                                                    currentState,
                                                    msg,
                                                    Math.Max(posStart, 0),
                                                    posEnd,
                                                    severity,
                                                    $"::sarif-linter-mod::{file.filename}",
                                                    fixIts.ToArray()
                                                ));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        Logger.Log($"Linter {Linter.executable} finished, found errors.");
                        break;
                    }
                    catch (OperationCanceledException) { process?.Kill(); Logger.Log(LogLevel.Error, "Linter timeout."); }
                    catch (Exception ex) { process?.Kill(); Logger.Log(LogLevel.Error, $"Linter error: {ex.Message}"); }
                }
            });
        }
    }
}