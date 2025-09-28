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

namespace EditorCore.Cursor
{
    public class EditorCursor
    {
        public List<EditorSelection> Selections { get; set; }
        public EditorBuffer Buffer { get; internal set; }
        List<Token> Tokens => Buffer.Tokens;

        internal EditorCursor(EditorBuffer buffer)
        {
            Buffer = buffer;
            Selections = [];
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
                        Selections = enumerable_result.Where(x => x is EditorSelection).Cast<EditorSelection>().ToList();
                    }
                    break;
                case "edit":
                    {
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
                                List<EditorSelection> newSelections = [];
                                foreach (var item in result)
                                {
                                    var s = new EditorSelection(this, begin);
                                    long endPosition = s.InsertText(item);
                                    s.SetPosition(begin, endPosition);
                                    begin = endPosition;
                                    newSelections.Add(s);
                                }
                                Selections.AddRange(newSelections);
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
                        foreach (Match x in Regex.Matches(value, command, RegexOptions.Singleline))
                        {
                            Selections.Add(new EditorSelection(this, index + x.Index, index + x.Index + x.Length));
                        }
                    }
                    break;
            }
            Selections.ForEach(x => x.UpdateFromLineOffset());
        }

        /* declarations for simplicity */

        public void Commit()
        {
            Buffer.OnUpdate();
        }

        public IEnumerable<string> SelectionsText => Selections.Select(x => x.Text.ToString());
    }
}
