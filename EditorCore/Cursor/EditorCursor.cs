using EditorCore.Buffer;
using EditorCore.File;
using EditorCore.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
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

        public void ApplyCommand(string type, string command)
        {
            switch (type)
            {
                case "edit":
                    Rope.Rope<char>[] args;
                    bool used_all_text = false;
                    if (Selections.Count == 0 || Selections.All(x => x.TextLength == 0))
                    {
                        args = [Buffer.Text];
                        used_all_text = true;
                    }
                    else
                    {
                        args = Selections.Select(x => x.Text).OfType<Rope.Rope<char>>().ToArray();
                    }
                    var result = Buffer.Server.CommandProvider.Execute(command, args.Select(x => x.ToString()).ToArray())?.ToArray();
                    if (result == null)
                    {
                        return;
                    }
                    Console.WriteLine($"get result: {string.Join(' ', result.Select(x => x.ToString()))}");
                    if (used_all_text)
                    {
                        Buffer.DeleteString(0, Buffer.Text.Length);
                    }
                    else
                    {
                        Selections.ForEach(x => Buffer.DeleteString(x.Min, x.TextLength));
                    }
                    {
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
                            if (Selections.Count == 0)
                            {
                                Selections.Add(new EditorSelection(this, 0));
                            }
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
                    break;
                case "find":
                    List<(long, string)> textFields = [];
                    foreach (EditorSelection selection in Selections)
                    {
                        if (selection.TextLength > 0)
                        {
                            textFields.Add((selection.Min, selection.Text.ToString()));
                        }
                    }
                    if (textFields.Count == 0)
                    {
                        textFields.Add((0, Buffer.Text.ToString()));
                    }
                    Selections.Clear();
                    foreach (var (index, value) in textFields)
                    {
                        foreach (Match x in Regex.Matches(value, command))
                        {
                            Selections.Add(new EditorSelection(this, index + x.Index, index + x.Index + x.Length));
                        }
                    }
                    break;
            }
        }

        /* declarations for simplicity */
    }
}
