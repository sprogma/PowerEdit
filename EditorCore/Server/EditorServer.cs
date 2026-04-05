using CommandProviderInterface;
using EditorCore.Buffer;
using EditorCore.File;
using Lsp;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;

namespace EditorCore.Server
{
    public class EditorServer
    {
        public ICommandProvider CommandProvider { get; internal set; }
        public Lock FilesLock = new();
        public List<EditorFile> Files { get; internal set; }

        Dictionary<string, Task<LspClient>> clients = [];
        public EditorBufferOnUpdate? ActionOnBufferUpdate;
        public EditorBufferOnTextInput? ActionOnBufferTextInput;
        public EditorFileOnSave? ActionOnFileSave;
        public int OpeningFiles = 0;

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
                OpeningFiles--;
            }
            return new_file;
        }

        public EditorFile CreateFile(string? name, string? languageId)
        {
            EditorFile new_file = new(this, new EditorBuffer(this, BaseTokenizer.CreateTokenizer(languageId), GetLspAsync(languageId), name, languageId, new PersistentCTextBuffer()))
            {
                filename = name
            };
            using (FilesLock.EnterScope())
            {
                Files.Add(new_file);
                OpeningFiles--;
            }
            return new_file;
        }

        public Task<LspClient>? GetLspAsync(string? languageId)
        {
            if (languageId != null && clients.TryGetValue(languageId, out var value))
            {
                return value;
            }
            switch (languageId)
            {
                case "c":
                    clients[languageId] = LspClient.StartAsync(Environment.CurrentDirectory, "clangd", "--offset-encoding=utf-8 --background-index --clang-tidy");
                    break;
                default:
                    return null;
            };
            return clients[languageId];
        }

        public void CloseFile(EditorFile file)
        {
            using (FilesLock.EnterScope())
            {
                Files.Remove(file);
                file.Dispose();
            }
        }

        /* declarations for simplicity */
    }
}
