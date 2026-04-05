using EditorCore.Buffer;
using Lsp;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TextBuffer;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EditorCore.File
{
    public delegate void EditorFileOnSave(EditorFile file);
    public class EditorFile : IDisposable
    {
        public EditorBuffer Buffer { get; internal set; }
        public Server.EditorServer Server { get; internal set; }

        public bool WasChanged => Buffer.WasChanged;

        public string? filename;
        public EditorFileOnSave? ActionOnSave = null;

        public EditorFile(Server.EditorServer server, string filename, ITextBuffer buffer)
        {
            this.filename = filename;
            Buffer = new EditorBuffer(server,
                                      BaseTokenizer.CreateTokenizer(EditorBuffer.LanguageId(filename)),
                                      server.GetLspAsync(EditorBuffer.LanguageId(filename)),
                                      this.filename, EditorBuffer.LanguageId(filename),
                                      buffer) {
                WasChanged = false
            };
            Server = server;

            ActionOnSave += server.ActionOnFileSave;
        }

        public EditorFile(Server.EditorServer server, EditorBuffer buffer)
        {
            this.filename = null;
            Buffer = buffer;
            Server = server;

            ActionOnSave += server.ActionOnFileSave;
        }

        public void Save(string? newFilename = null)
        {
            if (newFilename != null && newFilename != filename)
            {
                filename = newFilename;
                // update tokenizer
                string? ext = Path.GetExtension(newFilename)?.TrimStart('.') ?? "";
                Buffer.Tokenizer = BaseTokenizer.CreateTokenizer(ext);
                Buffer.Client = Server.GetLspAsync(LanguageId());
                Buffer.Filename = newFilename;
                Buffer.OnUpdate();
            }
            if (filename != null)
            {
                try
                {
                    Buffer.Text.SaveToFile(filename);
                    Buffer.WasChanged = false;
                }
                catch (Exception e)
                {
                    Buffer.WasChanged = true;
                    Console.Write($"Error: file wasn't saved: error {e.Message}");
                }
                ActionOnSave?.Invoke(this);
            }
        }

        ~EditorFile()
        {
            Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Buffer.Dispose();
        }

        public string? LanguageId(string name) => EditorBuffer.LanguageId(name);

        public string? LanguageId() => Buffer.LanguageId();
        /* declarations for simplicity */
    }
}
