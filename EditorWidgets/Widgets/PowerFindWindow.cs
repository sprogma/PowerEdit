using EditorCore.Buffer;
using EditorCore.Cursor;
using EditorCore.Server;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using Microsoft.PowerShell.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;

namespace EditorFramework.Widgets
{
    internal class PowerFindWindow : InputTextWindow
    {
        public EditorCursor usingCursor;

        internal static Dictionary<EditorCursor, List<string>> RequestsHistory = [];
        internal string Current;
        internal long currentHistoryPosition;


        public PowerFindWindow(IApplication app, ILayoutManager layout, EditorServer server, EditorCursor usingCursor) : 
                               base(app, layout, new EditorBuffer(server, usingCursor.Buffer.Tokenizer, null, usingCursor.Buffer.LanguageId(), new PersistentCTextBuffer()))
        {
            this.Current = "";
            buffer.SetText(Current);
            this.usingCursor = usingCursor;
            this.currentHistoryPosition = GetHistoryDepth();
        }

        private void Apply()
        {
            string cmd = buffer.Text.Substring(0);
            PushHistory(cmd);
            usingCursor.ApplyCommand("find", cmd);
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

