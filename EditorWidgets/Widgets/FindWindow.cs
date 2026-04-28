using EditorCore.Buffer;
using EditorCore.Cursor;
using EditorCore.Server;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TextBuffer;

namespace EditorFramework.Widgets
{
    public class FindWindow : InputTextWindow
    {
        [Flags]
        internal enum FindOptions {
            Reverse=0x1,
            Literal=0x2,
        }

        public EditorCursor usingCursor;

        internal static Dictionary<EditorCursor, List<string>> RequestsHistory = [];
        internal string Current;
        internal long currentHistoryPosition;

        internal FindOptions Options;

        public long? resultBegin, resultEnd;


        public FindWindow(IApplication app, ILayoutManager layout, EditorServer server, EditorCursor usingCursor) :
                          base(app, layout, new EditorBuffer(server, usingCursor.Buffer.Tokenizer, null, usingCursor.Buffer.LanguageId(), new PersistentCTextBuffer()))
        {
            this.Current = "";
            buffer.SetText(Current);
            this.usingCursor = usingCursor;
            this.currentHistoryPosition = GetHistoryDepth();
            this.resultBegin = null;
            this.resultEnd = null;
        }


        internal void UpdateResult()
        {
            if (usingCursor.Selections.Count == 0) return;

            string pattern = buffer.Text.Substring(0);
            string fullText = usingCursor.Buffer.Text.Substring(0);
            int startPos = (int)usingCursor.Selections[usingCursor.Selections.Count - 1].Begin;

            bool isReverse = (Options & FindOptions.Reverse) != 0;
            bool isLiteral = (Options & FindOptions.Literal) != 0;

            int foundIdx = -1;
            int length = 0;

            if (isLiteral)
            {
                StringComparison comp = StringComparison.Ordinal;
                if (isReverse)
                {
                    foundIdx = fullText.LastIndexOf(pattern, startPos - 1, comp);
                    if (foundIdx == -1) foundIdx = fullText.LastIndexOf(pattern, comp);
                }
                else
                {
                    foundIdx = fullText.IndexOf(pattern, startPos + 1, comp);
                    if (foundIdx == -1) foundIdx = fullText.IndexOf(pattern, comp);
                }
                length = pattern.Length;
            }
            else
            {
                string regexPattern = pattern;
                RegexOptions opt = isReverse ? RegexOptions.RightToLeft : RegexOptions.None;

                Match m = Regex.Match(fullText, regexPattern, opt);

                m = Regex.Match(fullText, regexPattern, opt);

                var matches = Regex.Matches(fullText, regexPattern, opt);
                if (matches.Count > 0)
                {
                    Match? bestMatch = null;
                    if (isReverse)
                    {
                        foreach (Match candidate in matches)
                            if (candidate.Index < startPos) { bestMatch = candidate; break; }
                        foundIdx = (bestMatch ?? matches[0]).Index;
                        length = (bestMatch ?? matches[0]).Length;
                    }
                    else
                    {
                        foreach (Match candidate in matches)
                            if (candidate.Index > startPos) { bestMatch = candidate; break; }
                        foundIdx = (bestMatch ?? matches[0]).Index;
                        length = (bestMatch ?? matches[0]).Length;
                    }
                }
            }

            if (foundIdx != -1)
            {
                resultBegin = foundIdx;
                resultEnd = foundIdx + length;
            }
            else
            {
                resultBegin = resultEnd = null;
            }
        }

        private void Apply()
        {
            string cmd = buffer.Text.Substring(0);
            PushHistory(cmd);

            UpdateResult();

            if (resultBegin != null && resultEnd != null)
            {
                usingCursor.Selections.Clear();
                usingCursor.Selections.Add(new(usingCursor, resultBegin.Value, resultEnd.Value));
            }
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
                case KeyChordEvent key when key.Is(KeyCode.R, KeyMode.Ctrl):
                    Options ^= FindOptions.Reverse;
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.L, KeyMode.Ctrl):
                    Options ^= FindOptions.Literal;
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
