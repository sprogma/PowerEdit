using CommandProviderInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;
using Lsp;

namespace EditorCore.Server
{
    public class EditorServer
    {
        public ICommandProvider CommandProvider { get; internal set; }
        public List<File.EditorFile> Files { get; internal set; }
        Dictionary<string, LspClient> clients = [];

        public EditorServer(ICommandProvider commandProvider)
        {
            CommandProvider = commandProvider;
            Files = [];
        }

        public File.EditorFile OpenFile(string filename)
        {
            File.EditorFile new_file = new File.EditorFile(this, filename, new PersistentCTextBuffer());
            Files.Add(new_file);
            return new_file;
        }

        public LspClient GetLsp(string v)
        {
            if (clients.TryGetValue(v, out LspClient? value))
            {
                return value;
            }
            clients[v] = new();
            Task.Run(() => clients[v].StartAsync("clangd", "--stdio"));
            return clients[v];
        }

        /* declarations for simplicity */
    }
}
