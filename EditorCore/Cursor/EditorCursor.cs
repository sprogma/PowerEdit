using EditorCore.Buffer;
using EditorCore.File;
using EditorCore.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace EditorCore.Cursor
{
    public class EditorCursor
    {
        public List<EditorSelection> Selections { get; set; }

        public EditorBuffer Buffer { get; internal set; }

        internal EditorCursor(EditorBuffer buffer)
        {
            Buffer = buffer;
            Selections = [];
        }

        public void ApplyCommand(string command)
        {
            Rope.Rope<char>[] args;
            if (Selections.Count == 0 || Selections.All(x => x.TextLength == 0))
            {
                args = [Buffer.Text];
                Buffer.Text = "";
            }
            else
            {
                args = Selections.Select(x => x.Text).OfType<Rope.Rope<char>>().ToArray();
                Selections.ForEach(x => Buffer.DeleteString(x.Min, x.TextLength));
            }
            var result = Buffer.Server.CommandProvider.Execute(command, args.Select(x => x.ToString()).ToArray())?.ToArray();
            if (result == null)
            {
                return;
            }
            Console.WriteLine($"get result: {string.Join(' ', result.Select(x => x.ToString()))}");
            int id = 0;
            if (result.Length == Selections.Count)
            {
                foreach (var item in result)
                {
                    Selections[id].Begin = Selections[id].End;
                    long begin = Selections[id].End;
                    Selections[id].InsertText(item);
                    /* select entered text */
                    Selections[id].SetPosition(begin, Selections[id].End);
                    id++;
                }
            }
            else
            {
                long begin = Selections[^1].End;
                Selections = [];
                foreach (var item in result)
                {
                    var s = new EditorSelection(this, begin);
                    s.InsertText(item);
                    s.SetPosition(begin, s.End);
                    begin = s.End;
                    Selections.Add(s);
                }
            }
        }

        /* declarations for simplicity */
    }
}
