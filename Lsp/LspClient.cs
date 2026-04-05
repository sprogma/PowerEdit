using Common;
using Common.AsyncHelper;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using System;
using System.Diagnostics;
using System.Linq;


// TODO: all this 

namespace Lsp
{
    internal struct LSPErrorMark : IErrorMark
    {
        public LSPErrorMark(string message, long begin, long end, ErrorMarkSeverity severity, string source)
        {
            Message = message;
            Begin = begin;
            End = end;
            Severity = severity;
            Source = source;
        }

        public string Message { get; init; }

        public long Begin { get; set; }
        public long End { get ; set; }

        public ErrorMarkSeverity Severity { get; init; }

        public string Source { get; init; }
    }

    public class LspClient : IDisposable
    {
        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        LanguageClient LanguageClient;
        Dictionary<string, int> Versions = [];
        Dictionary<string, Func<long, long, long>> PositionCallbacks;
        Dictionary<string, Action<string, IEnumerable<IErrorMark>>> Callbacks;
        Process ServerProcess;
        string ServerPath;
        string RootPath;

        LspClient(string rootPath,
                  string serverPath,
                  Process serverProcess,
                  LanguageClient languageClient,
                  Dictionary<string, Action<string, IEnumerable<IErrorMark>>> callbacks,
                  Dictionary<string, Func<long, long, long>> positionCallbacks)
        {
            Callbacks = callbacks;
            RootPath = rootPath;
            ServerPath = serverPath;
            ServerProcess = serverProcess;
            LanguageClient = languageClient;
            PositionCallbacks = positionCallbacks;
        }

        public virtual long MaxContentSize => 256 * 1024;

        public static async Task<LspClient> StartAsync(string rootPath, string serverPath, string? arguments, object optionObject)
        {
            Dictionary<string, Action<string, IEnumerable<IErrorMark>>> callbacks = [];
            Dictionary<string, Func<long, long, long>> positionCallbacks = [];

            Logger.Log("starting lsp");
            var serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = serverPath,
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            serverProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) { Logger.Log(Common.LogLevel.Warning, e.Data); } };
            serverProcess.Start();
            serverProcess.BeginErrorReadLine();
            Logger.Log("process started");
            var languageClient = OmniSharp.Extensions.LanguageServer.Client.LanguageClient.Create(options => options
                .WithInput(serverProcess.StandardOutput.BaseStream)
                .WithOutput(serverProcess.StandardInput.BaseStream)
                .WithRootPath(rootPath)
                .WithClientCapabilities(new ClientCapabilities
                {
                    General = new GeneralClientCapabilities
                    {
                        PositionEncodings = new(PositionEncodingKind.UTF8)
                    },
                    TextDocument = new TextDocumentClientCapabilities
                    {
                        Synchronization = new TextSynchronizationCapability
                        {
                            DynamicRegistration = true,
                            WillSave = true,
                            DidSave = true,
                        }
                    }
                })
                .WithInitializationOptions(optionObject)
                .OnPublishDiagnostics(paramsArgs => {
                    Logger.Log($"--- got errors for {paramsArgs.Uri} ---");
                    Dictionary<string, List<IErrorMark>> errors = [];
                    string uri = paramsArgs.Uri.ToString();
                    foreach (var err in paramsArgs.Diagnostics)
                    {
                        if (err == null) continue;

                        if (!errors.TryGetValue(uri, out List<IErrorMark>? value))
                        {
                            value = [];
                            errors[uri] = value;
                        }
                        if (positionCallbacks.TryGetValue(uri, out var pairToPos))
                        {
                            long begin = pairToPos(err.Range.Start.Line, err.Range.Start.Character);
                            long end = pairToPos(err.Range.End.Line, err.Range.End.Character);
                            value.Add(new LSPErrorMark($"{err.Message} - {err.Source}",
                                                                begin,
                                                                Math.Max(end, begin + 1),
                                                                (err.Severity switch
                                                                {
                                                                    DiagnosticSeverity.Information or DiagnosticSeverity.Hint => ErrorMarkSeverity.Note,
                                                                    DiagnosticSeverity.Warning => ErrorMarkSeverity.Waring,
                                                                    DiagnosticSeverity.Error => ErrorMarkSeverity.Error,
                                                                    _ => ErrorMarkSeverity.Note
                                                                }),
                                                                uri));
                        }
                        if (err.RelatedInformation is not null)
                        {
                            foreach (var rel in err.RelatedInformation)
                            {
                                string relUri = rel.Location.Uri.ToString();
                                if (positionCallbacks.TryGetValue(uri, out var relPairToPos))
                                {
                                    long begin = relPairToPos(rel.Location.Range.Start.Line, rel.Location.Range.Start.Character);
                                    long end = relPairToPos(rel.Location.Range.End.Line, rel.Location.Range.End.Character);
                                    errors[relUri].Add(new LSPErrorMark($"[note:] {rel.Message}",
                                                                    begin,
                                                                    Math.Max(begin + 1, end),
                                                                    ErrorMarkSeverity.Note,
                                                                    uri));
                                }
                            }
                        }
                    }
                    foreach (var (name, value) in callbacks)
                    {
                        if (errors.TryGetValue(uri, out var error))
                        {
                            value(uri, error);
                        }
                        else
                        {
                            value(uri, []);
                        }
                    }
                })
            );
            Logger.Log("lsp starting");
            await languageClient.Initialize(CancellationToken.None);
            Logger.Log("LSP initializated");
            return new(rootPath, serverPath, serverProcess, languageClient, callbacks, positionCallbacks);
        }

        private DocumentUri GetUri(string? filePath, string uniqueKey)
        {
            if (filePath != null)
            {
                return DocumentUri.FromFileSystemPath(filePath);
            }
            else
            {
                return DocumentUri.Parse($"file://virtual/inmemory/{uniqueKey}");
            }
        }

        public async Task OpenFileAsync(Action<string, IEnumerable<IErrorMark>> callback, Func<long, long, long> positionCallback, string? filePath, string uniqueKey, string? languageId, string content)
        {
            Logger.Log($"opening file {languageId}");
            if (languageId == null) return;
            var uri = GetUri(filePath, uniqueKey);
            Callbacks.Add(uri.ToString(), callback);
            PositionCallbacks.Add(uri.ToString(), positionCallback);
            Versions.Add(uri.ToString(), 1);
            LanguageClient.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = uri,
                    LanguageId = languageId,
                    Version = Versions[uri.ToString()],
                    Text = content
                }
            });
        }

        public async Task CloseFileAsync(string? filePath, string uniqueKey, string? languageId)
        {
            Logger.Log($"closing file {languageId}");
            if (languageId == null) return;
            var uri = GetUri(filePath, uniqueKey);
            Callbacks.Remove(uri.ToString());
            PositionCallbacks.Remove(uri.ToString());
            Versions.Remove(uri.ToString());
            LanguageClient.TextDocument.DidCloseTextDocument(new DidCloseTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = uri,
                }
            });
        }

        public async Task RenameFileAsync(string? oldFilePath, string? newFilePath, string uniqueKey, string? languageId, string content)
        {
            var oldUri = GetUri(oldFilePath, uniqueKey);
            var newUri = GetUri(newFilePath, uniqueKey);

            if (!Callbacks.TryGetValue(oldUri.ToString(), out var callback))
            {
                Logger.Log(Common.LogLevel.Error, "Can't rename file: it doesn't have callback.");
                throw new Exception("No callback");
            }
            if (!PositionCallbacks.TryGetValue(oldUri.ToString(), out var positionCallback))
            {
                Logger.Log(Common.LogLevel.Error, "Can't rename file: it doesn't have PositionCallbak.");
                throw new Exception("No position callback");
            }

            callback(oldUri.ToString(), []);

            await CloseFileAsync(oldFilePath, uniqueKey, languageId);

            LanguageClient.Workspace.DidChangeWatchedFiles(new DidChangeWatchedFilesParams
            {
                Changes = new[]
                {
                    new FileEvent { Uri = oldUri, Type = FileChangeType.Deleted },
                }
            });

            await OpenFileAsync(callback, positionCallback, newFilePath, uniqueKey, languageId, content);

            Logger.Log($"File renamed: {oldUri} -> {newUri}");
        }


        public async Task ChangeFileAsync(string? filePath, string uniqueKey, string newText)
        {
            var uri = GetUri(filePath, uniqueKey);
            Versions[uri.ToString()] += 1;
            LanguageClient.TextDocument.DidChangeTextDocument(new DidChangeTextDocumentParams
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Uri = uri,
                    Version = Versions[uri.ToString()]
                },
                ContentChanges = new[] {
                    new TextDocumentContentChangeEvent {
                        Text = newText
                    }
                }
            });
            Logger.Log($"Update sent full text = {newText}");
        }

        public async Task ChangeFileAsync(string? filePath, string uniqueKey, int line, int col, string insertedText)
        {
            var uri = GetUri(filePath, uniqueKey);
            Versions[uri.ToString()] += 1;
            LanguageClient.TextDocument.DidChangeTextDocument(new DidChangeTextDocumentParams
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Uri = uri,
                    Version = Versions[uri.ToString()]
                },
                ContentChanges = new Container<TextDocumentContentChangeEvent>(
                    new TextDocumentContentChangeEvent
                    {
                        Range = new(
                            new(line, col),
                            new(line, col)
                        ),
                        RangeLength = 0,
                        Text = insertedText
                    }
                )
            });
            Logger.Log($"Update sent Insert text = {insertedText} at {line}:{col}");
        }

        public async Task ChangeFileAsync(string? filePath, string uniqueKey, int line, int col, int end_line, int end_col, int total_length)
        {
            var uri = GetUri(filePath, uniqueKey);
            Versions[uri.ToString()] += 1;
            LanguageClient.TextDocument.DidChangeTextDocument(new DidChangeTextDocumentParams
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Uri = uri,
                    Version = Versions[uri.ToString()]
                },
                ContentChanges = new Container<TextDocumentContentChangeEvent>(
                    new TextDocumentContentChangeEvent
                    {
                        Range = new(
                            new(line, col),
                            new(end_line, end_col)
                        ),
                        Text = ""
                    }
                )
            });
            Logger.Log($"Update sent Delete text at {line}:{col} to {end_line}:{end_col} (count={total_length})");
        }

        public async Task<(string value, string label, string kind)[]> GetCompletionsAsync(string? filePath, string uniqueKey, int line, int col)
        {
            var uri = GetUri(filePath, uniqueKey);
            var completionList = await LanguageClient.TextDocument.RequestCompletion(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Position = new Position(line, col),
                Context = new CompletionContext
                {
                    TriggerKind = CompletionTriggerKind.Invoked
                }
            });

            List<(string, string, string)> res = [];
            foreach (var item in completionList)
            {
                res.Add((item.InsertText ?? "", item.Label, item.Kind.ToString()));
                Logger.Log($"Variant: {item.Label} [{item.InsertText}]");
            }
            return [.. res];
        }

        public void Dispose()
        {
            AsyncHelper.RunSync(LanguageClient.Shutdown);
            ServerProcess.Kill();
        }
    }

}
