using EditorCore.File;
using EditorCore.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EditorCore.Cursor
{
    public class EditorCursor
    {
        public List<EditorSelection> Selections { get; private set; }

        public EditorFile File { get; private set; }

        public EditorCursor(EditorFile file)
        {
            File = file;
            Selections = [];
        }

        public void Execute(string command)
        {
            throw new NotImplementedException();
        }

        /* declarations for simplicity */
    }
}
