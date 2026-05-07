using EditorCore.Buffer;
using EditorCore.Cursor;
using EditorCore.Selection;
using EditorCore.Server;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;

namespace EditorFramework.Widgets
{
    public class PowerEditWindow: InputTextWindow
    {
        public EditorCursor usingCursor;
        private readonly string editType;

        internal static Dictionary<EditorCursor, List<string>> RequestsHistory = [];
        internal string Current;
        internal long currentHistoryPosition;


        public (IEnumerable<string>?, string?) CurrentResult()
        {
            if (editType == "powerEdit")
            {
                var countToTake = Math.Min(usingCursor.Selections.Count, 128);
                var selectionsToProcess = usingCursor.Selections.Take((int)countToTake).ToArray();
                var res = buffer.Server.CommandProvider.Execute(buffer.Text.Substring(0), selectionsToProcess);
                var results = res.Item1?.Select(x => x.ToString()).Where(x => x != null).Cast<string>() ?? [];
                if (usingCursor.Selections.Count > 128)
                {
                    results = results.Prepend("Preview limited to first 128 nodes");
                }
                return (results, res.Item2);
            }
            else if (editType == "replace")
            {
                string[] text;
                if (usingCursor.Selections.All(x => x.TextLength == 0))
                {
                    text = [usingCursor.Buffer.Text.Substring(0)];
                }
                else
                {
                    text = usingCursor.SelectionsText.ToArray();
                }
                long currentTotal = 0;
                var limitedText = text.TakeWhile(s => { currentTotal += s.Length; return currentTotal <= 16384; }).ToArray();

                bool isTruncated = limitedText.Length < text.Length;
                var res = buffer.Server.CommandProvider.Execute(buffer.Text.Substring(0), limitedText);
                var results = res.Item1?.Select(x => x?.ToString()).Where(x => x != null).Cast<string>() ?? [];

                if (isTruncated)
                {
                    results = results.Prepend("Too big count of args. Preview cutted");
                }
                return (results, res.Item2);
            }
            else
            {
                var text = usingCursor.SelectionsText.ToArray();
                long currentTotal = 0;
                var limitedText = text.TakeWhile(s => { currentTotal += s.Length; return currentTotal <= 16384; }).ToArray();
                bool isTruncated = limitedText.Length < text.Length;
                var res = buffer.Server.CommandProvider.Execute(buffer.Text.Substring(0), limitedText);
                var results = res.Item1?.Select(x => x?.ToString()).Where(x => x != null).Cast<string>() ?? [];
                if (isTruncated)
                {
                    results = results.Prepend("Too big count of args. Preview cutted");
                }
                return (results, res.Item2);

            }
        }

        public PowerEditWindow(IApplication app, ILayoutManager layout, EditorServer server, EditorCursor usingCursor, string editType) : 
                               base(app, layout, new EditorBuffer(server, server.CommandProvider.Tokenizer, null, server.CommandProvider.LanguageId, new PersistentCTextBuffer()).Cursor)
        {
            (long begin, long end, string text) = server.CommandProvider.ExampleScript(editType);
            Current = text;
            buffer.SetText(Current);
            cursor?.Selections = new(cursor, [new EditorSelection(cursor, begin, end)]);
            this.usingCursor = usingCursor;
            this.editType = editType;
            this.currentHistoryPosition = GetHistoryDepth();
        }

        internal void Apply()
        {
            string cmd = buffer.Text.Substring(0);
            PushHistory(cmd);
            usingCursor.Fork();
            if (editType == "powerEdit")
            {
                usingCursor.ApplyCommand("powerEdit", cmd);
            }
            else if (editType == "replace")
            {
                usingCursor.ApplyCommand("replace", cmd);
            }
            else
            {
                usingCursor.ApplyCommand("edit", cmd);
            }
            usingCursor.Commit();
        }

        private void TouchHistory()
        {
            if (!RequestsHistory.ContainsKey(usingCursor))
            {
                RequestsHistory[usingCursor] = [];
            }
        }

        private void PushHistory(string value)
        {
            TouchHistory();
            if (!string.IsNullOrEmpty(value))
            {
                if (RequestsHistory[usingCursor].Count == 0 || RequestsHistory[usingCursor][^1] != value)
                {
                    RequestsHistory[usingCursor].Add(value);
                }
            }
        }

        private string GetHistory(long id)
        {
            TouchHistory();
            if (id >= RequestsHistory[usingCursor].Count)
            {
                return Current;
            }
            return RequestsHistory[usingCursor][(int)id];
        }

        private long GetHistoryDepth()
        {
            TouchHistory();
            return RequestsHistory[usingCursor].Count;
        }


        public override bool HandleEvent(EventBase e)
        {
            switch (e)
            {
                case QuitEvent:
                    Environment.Exit(1);
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Escape):
                    DeleteSelf();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Enter, KeyMode.Ctrl):
                    Apply();
                    DeleteSelf();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Up, KeyMode.Ctrl):
                    if (currentHistoryPosition == GetHistoryDepth())
                    {
                        Current = buffer.Text.Substring(0);
                    }
                    currentHistoryPosition = Math.Max(0, currentHistoryPosition - 1);
                    buffer.SetText(GetHistory(currentHistoryPosition));
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Down, KeyMode.Ctrl):
                    currentHistoryPosition = Math.Min(GetHistoryDepth(), currentHistoryPosition + 1);
                    buffer.SetText(GetHistory(currentHistoryPosition));
                    return false;
            }
            return base.HandleEvent(e);
        }
    }
}

