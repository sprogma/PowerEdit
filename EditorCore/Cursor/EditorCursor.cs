using EditorCore.Buffer;
using EditorCore.File;
using EditorCore.Selection;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Transactions;
using TextBuffer;

namespace EditorCore.Cursor
{
    public class EditorCursor
    {
        public EditorSelectionList Selections { get; set; }
        public EditorBuffer Buffer { get; internal set; }

        internal EditorCursor(EditorBuffer buffer)
        {
            Buffer = buffer;
            Selections = new(this);
        }

        public void ApplyCommand(string type, string command)
        {
            switch (type)
            {
                case "powerEdit":
                    {
                        (var enumerable_result, string? error_string) = Buffer.Server.CommandProvider.Execute(command, Selections.ToArray());
                        if (enumerable_result == null)
                        {
                            return;
                        }
                        Console.WriteLine($"get result: {string.Join(' ', enumerable_result.Select(x => x.ToString()))}");
                        Selections = new(this, enumerable_result.Where(x => x is EditorSelection).Cast<EditorSelection>().ToArray());
                    }
                    break;
                case "replace":
                    {
                        string[] args;
                        if (Selections.All(x => x.TextLength == 0))
                        {
                            args = [Buffer.Text.Substring(0)];
                            if (Buffer.Text is IEditableTextBuffer editableText)
                            {
                                editableText.Clear();
                            }
                        }
                        else
                        {
                            args = Selections.Select(x => x.Text).OfType<string>().ToArray();
                        }
                        (var enumerable_result, string? error_string) = Buffer.Server.CommandProvider.Execute(command, args.Select(x => x.ToString()).ToArray());
                        var result = enumerable_result?.Select(x => x.ToString()).
                                                        Where(x => x != null).
                                                        Cast<string>().
                                                        ToArray();
                        if (result == null)
                        {
                            return;
                        }
                        Console.WriteLine($"get result: {string.Join(' ', result.Select(x => x.ToString()))}");
                        foreach (var x in Selections)
                        {
                            Buffer.DeleteString(x.Min, x.TextLength);
                        }
                        {
                            int id = 0;
                            if (result.Length == Selections.Count)
                            {
                                foreach (var item in result)
                                {
                                    Selections[id].Begin = Selections[id].End;
                                    long begin = Selections[id].End;
                                    Selections[id].InsertString(item);
                                    /* select entered text */
                                    Selections[id].SetPosition(begin, Selections[id].End);
                                    id++;
                                }
                            }
                            else
                            {
                                if (Selections.Count == 0)
                                {
                                    Selections.Insert(Selections.Count, new EditorSelection(this, 0));
                                }
                                long begin = Selections[Selections.Count - 1].End;
                                List<EditorSelection> newSelections = [];
                                foreach (var item in result)
                                {
                                    var s = new EditorSelection(this, begin);
                                    long endPosition = s.InsertString(item);
                                    s.SetPosition(begin, endPosition);
                                    begin = endPosition;
                                    newSelections.Add(s);
                                }
                                Selections = new(this, newSelections);
                            }
                        }
                    }
                    break;
                case "edit":
                    {
                        string[] args = Selections.Select(x => x.Text).OfType<string>().ToArray();
                        (var enumerable_result, string? error_string) = Buffer.Server.CommandProvider.Execute(command, args.Select(x => x.ToString()).ToArray());
                        var result = enumerable_result?.Select(x => x.ToString()).
                                                        Where(x => x != null).
                                                        Cast<string>().
                                                        ToArray();
                        if (result == null)
                        {
                            return;
                        }
                        Console.WriteLine($"get result: {string.Join(' ', result.Select(x => x.ToString()))}");
                        foreach (var x in Selections)
                        {
                            Buffer.DeleteString(x.Min, x.TextLength);
                        }
                        {
                            int id = 0;
                            if (result.Length == Selections.Count)
                            {
                                foreach (var item in result)
                                {
                                    Selections[id].Begin = Selections[id].End;
                                    long begin = Selections[id].End;
                                    Selections[id].InsertString(item);
                                    /* select entered text */
                                    Selections[id].SetPosition(begin, Selections[id].End);
                                    id++;
                                }
                            }
                            else
                            {
                                if (Selections.Count == 0)
                                {
                                    Selections.Insert(Selections.Count, new EditorSelection(this, 0));
                                }
                                long begin = Selections[Selections.Count-1].End;
                                List<EditorSelection> newSelections = [];
                                foreach (var item in result)
                                {
                                    var s = new EditorSelection(this, begin);
                                    long endPosition = s.InsertString(item);
                                    s.SetPosition(begin, endPosition);
                                    begin = endPosition;
                                    newSelections.Add(s);
                                }
                                Selections = new(this, newSelections);
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
                        textFields.Add((0, Buffer.Text.Substring(0)));
                    }
                    Selections.Clear();
                    try
                    {
                        foreach (var (index, value) in textFields)
                        {
                            var result = Regex.Matches(value, command, RegexOptions.Singleline);
                            foreach (Match x in result)
                            {
                                Selections.Insert(Selections.Count, new EditorSelection(this, index + x.Index, index + x.Index + x.Length));
                            }
                        }
                    }
                    catch 
                    {
                        Selections.Clear();
                    }
                    break;
            }
            Selections.UpdateFromOffset();
        }

        /* declarations for simplicity */

        public void Fork()
        {
            Buffer.Fork();
        }

        public void Commit()
        {
            Buffer.Commit();
            Selections.UpdateFromOffset();
        }

        public IEnumerable<string> SelectionsText => Selections.Select(x => x.Text.ToString());
    }
}
