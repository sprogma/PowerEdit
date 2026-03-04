using EditorCore.Buffer;
using Lsp;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EditorCore.File
{
    public delegate void EditorFileOnSave(EditorFile file);
    public class EditorFile
    {
        public EditorBuffer Buffer { get; internal set; }
        public Server.EditorServer Server { get; internal set; }

        IntPtr last_saved_version;

        public bool WasChanged => Buffer.WasChanged;

        public string? filename;
        public EditorFileOnSave? ActionOnSave = null;

        public EditorFile(Server.EditorServer server, string filename, ITextBuffer buffer)
        {
            this.filename = filename;
            Buffer = new EditorBuffer(server,
                                      System.IO.File.ReadAllText(filename),
                                      BaseTokenizer.CreateTokenizer(Path.GetExtension(filename)?.TrimStart('.') ?? ""),
                                      server.GetLsp(Path.GetExtension(filename)?.TrimStart('.') ?? ""),
                                      this.filename,
                                      buffer)
            {
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
                Buffer.Client = Server.GetLsp(ext);
                Buffer.FilePath = newFilename;
                Buffer.OnUpdate();
            }
            if (filename != null)
            {
                try
                {
                    Buffer.Text.SaveToFile(filename);
                    Buffer.WasChanged = false;
                }
                catch
                {
                    Buffer.WasChanged = true;
                }
                ActionOnSave?.Invoke(this);
            }
        }

        /* declarations for simplicity */
    }
}
