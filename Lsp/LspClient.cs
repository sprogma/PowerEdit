using LoggingLogLevel;
using System.Diagnostics;



// TODO: all this 

namespace Lsp
{
    public class LspClient : IDisposable
    {
        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        public virtual long MaxContentSize => 256 * 1024;

        public async Task StartAsync(string serverPath, string arguments)
        {
            Logger.Log("Server connected.");
            _tcs.SetResult(true);
        }

        public async Task OpenFileAsync(string filePath, string languageId, string content)
        {
            await _tcs.Task;
        }

        public async Task ChangeFileAsync(string filePath, string newText)
        {
            await _tcs.Task;
        }

        public void Dispose()
        {
        }
    }

}
