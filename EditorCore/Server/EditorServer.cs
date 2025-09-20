using CommandProviderInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EditorCore.Server
{
    public class EditorServer
    {
        public ICommandProvider CommandProvider { get; internal set; }
        public List<File.EditorFile> Files { get; internal set; }

        public EditorServer(ICommandProvider commandProvider)
        {
            CommandProvider = commandProvider;
            Files = [];
        }

        public File.EditorFile OpenFile(string filename)
        {
            File.EditorFile new_file = new File.EditorFile(this, filename);
            Files.Add(new_file);
            return new_file;
        }

        /* declarations for simplicity */
    }
}
