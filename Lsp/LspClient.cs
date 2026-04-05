using Logging;
using Logging.AsyncHelper;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Diagnostics;
using System.Linq;


// TODO: all this 

namespace Lsp
{
    public enum LSPSeverity
    {
        Note,
        Waring,
        Error,
    }

    public record LSPDiagnostic(string Message, 
                         string? Source,
                         int StartLine, int StartColumn,
                         int EndLine, int EndColumn,
                         LSPSeverity Severity,
                         (string Message,
                         int StartLine, int StartColumn,
                         int EndLine, int EndColumn)[] Info
    );

    public class LspClient : IDisposable
    {
        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        LanguageClient languageClient;
        Dictionary<string, int> Versions = [];
        Dictionary<string, Action<string, LSPDiagnostic[]>> Callbacks = [];
        Process ServerProcess;
        string ServerPath;
        string RootPath;

        LspClient(string rootPath, string serverPath, Process serverProcess, LanguageClient languageClient, Dictionary<string, Action<string, LSPDiagnostic[]>> callbacks)
        {
            Callbacks = callbacks;
            RootPath = rootPath;
            ServerPath = serverPath;
            ServerProcess = serverProcess;
            this.languageClient = languageClient;
        }

        public virtual long MaxContentSize => 256 * 1024;

        public static async Task<LspClient> StartAsync(string rootPath, string serverPath, string? arguments)
        {
            Dictionary<string, Action<string, LSPDiagnostic[]>> callbacks = [];

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
            serverProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) { Logger.Log(Logging.LogLevel.Warning, e.Data); } };
            serverProcess.Start();
            serverProcess.BeginErrorReadLine();
            Logger.Log("process started");
            var languageClient = LanguageClient.Create(options => options
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
                            DidSave = true
                        }
                    }
                })
                .OnPublishDiagnostics(paramsArgs => {
                    Logger.Log($"--- got errors for {paramsArgs.Uri} ---");
                    if (callbacks.TryGetValue(paramsArgs.Uri.ToString(), out var value))
                    {
                        value(paramsArgs.Uri.ToString(),
                              paramsArgs.Diagnostics.Select(diagnostic =>
                                  new LSPDiagnostic(diagnostic.Message, 
                                                    diagnostic.Source,
                                                    diagnostic.Range.Start.Line, diagnostic.Range.Start.Character,
                                                    diagnostic.Range.End.Line, diagnostic.Range.End.Character,
                                                    (diagnostic.Severity switch { 
                                                        DiagnosticSeverity.Information or DiagnosticSeverity.Hint => LSPSeverity.Note,
                                                        DiagnosticSeverity.Warning => LSPSeverity.Waring,
                                                        DiagnosticSeverity.Error => LSPSeverity.Error,
                                                        _ => LSPSeverity.Note
                                                    }),
                                                    diagnostic.RelatedInformation?.Select(x => (x.Message,
                                                                                               x.Location.Range.Start.Line, x.Location.Range.Start.Character, 
                                                                                               x.Location.Range.End.Line, x.Location.Range.End.Character)).ToArray() ?? []
                              )).ToArray()
                        );
                    }
                })
            );
            Logger.Log("lsp starting");
            await languageClient.Initialize(CancellationToken.None);
            Logger.Log("LSP initializated");
            return new(rootPath, serverPath, serverProcess, languageClient, callbacks);
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

        public async Task OpenFileAsync(Action<string, LSPDiagnostic[]> callback, string? filePath, string uniqueKey, string? languageId, string content)
        {
            Logger.Log($"opening file {languageId}");
            if (languageId == null) return;
            var uri = GetUri(filePath, uniqueKey);
            Callbacks.Add(uri.ToString(), callback);
            Versions.Add(uri.ToString(), 1);
            languageClient.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = uri,
                    LanguageId = languageId,
                    Version = Versions[uri.ToString()],
                    Text = content
                }
            });
            Logger.Log("File opened");
        }

        public async Task ChangeFileAsync(string? filePath, string uniqueKey, string newText)
        {
            var uri = GetUri(filePath, uniqueKey);
            Versions[uri.ToString()] += 1;
            languageClient.TextDocument.DidChangeTextDocument(new DidChangeTextDocumentParams
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
            Logger.Log("Update sent 1");
        }

        public async Task ChangeFileAsync(string? filePath, string uniqueKey, int line, int col, string insertedText)
        {
            var uri = GetUri(filePath, uniqueKey);
            Versions[uri.ToString()] += 1;
            languageClient.TextDocument.DidChangeTextDocument(new DidChangeTextDocumentParams
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
            Logger.Log("Update sent 2");
        }

        public async Task ChangeFileAsync(string? filePath, string uniqueKey, int line, int col, int end_line, int end_col, int total_length)
        {
            var uri = GetUri(filePath, uniqueKey);
            Versions[uri.ToString()] += 1;
            languageClient.TextDocument.DidChangeTextDocument(new DidChangeTextDocumentParams
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
                        RangeLength = total_length,
                        Text = ""
                    }
                )
            });
            Logger.Log("Update sent 3");
        }

        public async Task<(string value, string label, string kind)[]> GetCompletionsAsync(string? filePath, string uniqueKey, int line, int col)
        {
            var uri = GetUri(filePath, uniqueKey);
            var completionList = await languageClient.TextDocument.RequestCompletion(new CompletionParams
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
            AsyncHelper.RunSync(languageClient.Shutdown);
            ServerProcess.Kill();
        }
    }

}
