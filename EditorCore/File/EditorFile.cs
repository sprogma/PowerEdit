using EditorCore.Buffer;
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
    public class EditorFile
    {
        public EditorBuffer Buffer { get; internal set; }
        public Server.EditorServer Server { get; internal set; }

        string? filename;

        public EditorFile(Server.EditorServer server, string filename, ITextBuffer buffer)
        {
            this.filename = filename;
            Buffer = new EditorBuffer(server, 
                                      System.IO.File.ReadAllText(filename), 
                                      BaseTokenizer.CreateTokenizer(Path.GetExtension(filename).Substring(1) ?? ""),
                                      buffer);
            Server = server;
        }

        public void Save(string? newFilename = null)
        {
            if (newFilename != null)
            {
                filename = newFilename;
            }
            if (filename != null)
            {
                Buffer.Text.SaveToFile(filename);
            }
        }

        /* declarations for simplicity */
    }
}
