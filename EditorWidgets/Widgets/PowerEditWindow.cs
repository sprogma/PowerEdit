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
        string editType;

        public (IEnumerable<string>?, string?) CurrentResult()
        {
            if (editType == "powerEdit")
            {
                var res = buffer.Server.CommandProvider.Execute(buffer.Text.Substring(0), usingCursor.Selections.ToArray());
                return (res.Item1?.Select(x => x.ToString()).Where(x => x != null).Cast<string>(), res.Item2);
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
                (IEnumerable<object?>?, string?) res;
                if (text.Sum(x => x.Length) > 1000)
                {
                    res = (["Too big arguments. preview disabled"], "Too big arguments. preview disabled");
                }
                else
                {    
                    res = buffer.Server.CommandProvider.Execute(buffer.Text.Substring(0), text);
                }
                return (res.Item1?.Select(x => x?.ToString()).Where(x => x != null).Cast<string>(), res.Item2);
            }
            else
            {
                var text = usingCursor.SelectionsText.ToArray();
                (IEnumerable<object?>?, string?) res;
                if (text.Sum(x => x.Length) > 1000)
                {
                    res = (["Too big arguments. preview disabled"], "Too big arguments. preview disabled");
                }
                else
                {
                    res = buffer.Server.CommandProvider.Execute(buffer.Text.Substring(0), text);
                }
                return (res.Item1?.Select(x => x?.ToString()).Where(x => x != null).Cast<string>(), res.Item2);
            }
        }

        public PowerEditWindow(IApplication app, ILayoutManager layout, EditorServer server, EditorCursor usingCursor, string editType) : 
                               base(app, layout, new EditorBuffer(server, server.CommandProvider.Tokenizer, null, "", server.CommandProvider.LanguageId, new PersistentCTextBuffer()))
        {
            (long begin, long end, string text) = server.CommandProvider.ExampleScript(editType);
            buffer.SetText(text);
            cursor?.Selections = new(cursor, [new EditorSelection(cursor, begin, end)]);
            this.usingCursor = usingCursor;
            this.editType = editType;
        }

        internal void Apply()
        {
            usingCursor.Fork();
            if (editType == "powerEdit")
            {
                usingCursor.ApplyCommand("powerEdit", buffer.Text.Substring(0));
            }
            else if (editType == "replace")
            {
                usingCursor.ApplyCommand("replace", buffer.Text.Substring(0));
            }
            else
            {
                usingCursor.ApplyCommand("edit", buffer.Text.Substring(0));
            }
            usingCursor.Commit();
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
            }
            return base.HandleEvent(e);
        }
    }
}

