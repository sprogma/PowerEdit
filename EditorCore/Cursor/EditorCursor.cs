using EditorCore.Buffer;
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
        public List<EditorSelection> Selections { get; internal set; }

        public EditorBuffer Buffer { get; internal set; }

        public EditorCursor(EditorBuffer buffer)
        {
            Buffer = buffer;
            Selections = [];
        }

        public void Execute(string command)
        {
            throw new NotImplementedException();
        }

        /* declarations for simplicity */
    }
}
