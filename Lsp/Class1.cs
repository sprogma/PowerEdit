using System.Diagnostics;
using LspTypes;
using StreamJsonRpc;

namespace Lsp
{
    public class LspClient
    {
        private JsonRpc _rpc;
        private Process _serverProcess;

        public async Task StartAsync(string serverPath)
        {
            _serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo(serverPath)
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            _serverProcess.Start();

            _rpc = JsonRpc.Attach(_serverProcess.StandardInput.BaseStream,
                                  _serverProcess.StandardOutput.BaseStream);
            _rpc.StartListening();

            var initParams = new InitializeParams
            {
                //Capabilities = new ClientCapabilities
                //{
                //    TextDocument = new TextDocumentClientCapabilities
                //    {
                //        Hover = new HoverClientCapabilities { DynamicRegistration = true }
                //    }
                //},
                ProcessId = Environment.ProcessId,
                RootUri = new Uri("file:///C:/MyProject/")
            };

            var result = await _rpc.InvokeWithParameterObjectAsync<InitializeResult>("initialize", initParams);
            await _rpc.NotifyAsync("initialized");
        }

        public async Task OpenDocumentAsync(string fileUri, string content)
        {
            var didOpenParams = new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = fileUri,
                    LanguageId = "csharp",
                    Version = 1,
                    Text = content
                }
            };

            await _rpc.NotifyWithParameterObjectAsync("textDocument/didOpen", didOpenParams);
        }
    }

}
