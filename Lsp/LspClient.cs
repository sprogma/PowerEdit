using System.Diagnostics;
using LspTypes;
using StreamJsonRpc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Lsp
{
    public class LspClient : IDisposable
    {
        private Process? _serverProcess;
        private JsonRpc? _rpc;
        private int _version = 1;

        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        public async Task StartAsync(string serverPath, string arguments)
        {
            _serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = serverPath,
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _serverProcess.Start();

            var formatter = new JsonMessageFormatter();
            formatter.JsonSerializer.ContractResolver = new CamelCasePropertyNamesContractResolver();

            var handler = new HeaderDelimitedMessageHandler(_serverProcess.StandardInput.BaseStream, _serverProcess.StandardOutput.BaseStream, formatter);
            _rpc = new JsonRpc(handler);

            _rpc.AddLocalRpcMethod("window/logMessage", (Action<LogMessageParams>)(p => Console.WriteLine($"[SERVER]: {p.Message}")));

            _rpc.AddLocalRpcMethod("textDocument/publishDiagnostics", (Action<Newtonsoft.Json.Linq.JToken>)(p => {
                Console.WriteLine($"[DIAG] Received diagnostics JSON: {p.ToString(Formatting.None)} of type {p.GetType()}");
            }));

            _rpc.StartListening();

            var initParams = new InitializeParams
            {
                ProcessId = Environment.ProcessId,
                RootUri = new Uri(Directory.GetCurrentDirectory()),
                InitializationOptions = new ClientCapabilities
                {
                    TextDocument = new TextDocumentClientCapabilities
                    {
                        Synchronization = new TextDocumentSyncClientCapabilities
                        {
                            DidSave = true
                        }
                    }
                }
            };

            await _rpc.InvokeWithParameterObjectAsync<InitializeResult>("initialize", initParams);
            await _rpc.NotifyAsync("initialized", new InitializedParams());

            Console.WriteLine("[LOG] Server connected.");
            _tcs.SetResult(true);
        }

        public async Task OpenFileAsync(string filePath, string languageId, string content)
        {
            await _tcs.Task;
            var fileUri = new Uri(filePath).AbsoluteUri;

            Console.WriteLine($"Using uri {fileUri}");

            var @params = new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = fileUri,
                    LanguageId = languageId,
                    Version = _version++,
                    Text = content
                }
            };

            await _rpc!.NotifyWithParameterObjectAsync("textDocument/didOpen", @params);
        }

        public async Task ChangeFileAsync(string filePath, string newText)
        {
            await _tcs.Task;
            var fileUri = new Uri(filePath).AbsoluteUri;

            var @params = new DidChangeTextDocumentParams
            {
                TextDocument = new VersionedTextDocumentIdentifier
                {
                    Uri = fileUri,
                    Version = _version++
                },
                ContentChanges = [
                    new TextDocumentContentChangeEvent { Text = newText }
                ]
            };

            await _rpc!.NotifyWithParameterObjectAsync("textDocument/didChange", @params);
        }

        public void Dispose()
        {
            _rpc?.Dispose();
            _serverProcess?.Kill();
            _serverProcess?.Dispose();
        }
    }

}
