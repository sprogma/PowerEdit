using EditorCore.Buffer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EditorCore.File
{
    public class EditorFile
    {
        public EditorBuffer Buffer { get; internal set; }
        public Server.EditorServer Server { get; internal set; }

        public EditorFile(Server.EditorServer server, string filename)
        {
            Buffer = new EditorBuffer(server, System.IO.File.ReadAllText(filename));
            Server = server;
        }

        public void Save(string filename)
        {
            System.IO.File.WriteAllText(filename, Buffer.Text.ToString());
        }

        /* declarations for simplicity */
    }
}
