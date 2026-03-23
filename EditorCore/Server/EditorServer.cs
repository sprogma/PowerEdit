using CommandProviderInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;
using Lsp;
using EditorCore.Buffer;
using EditorCore.File;
using RegexTokenizer;

namespace EditorCore.Server
{
    public class EditorServer
    {
        public ICommandProvider CommandProvider { get; internal set; }
        public Lock FilesLock = new();
        public List<EditorFile> Files { get; internal set; }

        Dictionary<string, LspClient> clients = [];
        public EditorBufferOnUpdate? ActionOnBufferUpdate;
        public EditorBufferOnTextInput? ActionOnBufferTextInput;
        public EditorFileOnSave? ActionOnFileSave;

        public EditorServer(ICommandProvider commandProvider)
        {
            CommandProvider = commandProvider;
            Files = [];
        }

        public EditorFile OpenFile(string filename)
        {
            EditorFile new_file = new(this, filename, new PersistentCTextBuffer(filename));
            using (FilesLock.EnterScope())
            {
                Files.Add(new_file);
            }
            return new_file;
        }

        public EditorFile CreateFile(string? name, string? externsion)
        {
            EditorFile new_file = new(this, new EditorBuffer(this, BaseTokenizer.CreateTokenizer(externsion), GetLsp(externsion), name, new PersistentCTextBuffer()))
            {
                filename = name
            };
            using (FilesLock.EnterScope())
            {
                Files.Add(new_file);
            }
            return new_file;
        }

        public LspClient? GetLsp(string? v)
        {
            if (v == null) return null;
            if (clients.TryGetValue(v, out LspClient? value))
            {
                return value;
            }
            clients[v] = new();
            //Task.Run(() => clients[v].StartAsync("clangd", "--stdio"));
            return clients[v];
        }

        /* declarations for simplicity */
    }
}
