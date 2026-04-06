using EditorCore.Buffer;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;

namespace EditorFramework.Widgets
{
    public class PowerEditWithPreviewWindow : BaseWindow
    {
        public PowerEditWindow editor;
        public SimpleTextWindow preview;
        public DateTime lastDrawTime;
        public bool moditifed;

        public PowerEditWithPreviewWindow(IApplication app, ILayoutManager layout, PowerEditWindow editor) : base(app, layout)
        {
            moditifed = true;
            editor.buffer.ActionOnUpdate +=  buf => {moditifed = true; };
            this.editor = editor;
            this.preview = new(app, GetLayout<SimpleTextWindow>.Value, new EditorBuffer(editor.buffer.Server, "processing ...", editor.usingCursor.Buffer.Tokenizer, null, editor.usingCursor.Buffer.LanguageId(), new ReadonlyTextBuffer()) { TryUseLSP = false });
            this.lastDrawTime = DateTime.UtcNow;
        }

        public override void PreDraw()
        {
            base.PreDraw();
            if ((DateTime.UtcNow - lastDrawTime).TotalSeconds > 1 && moditifed)
            {
                lastDrawTime = DateTime.UtcNow;
                moditifed = false;
                /* update result */

                Thread thread = new Thread(() =>
                {
                    (var res, string? error_string) = editor.CurrentResult();
                    if (res == null)
                    {
                        preview.buffer.SetText($"-> Error:\n{error_string}");
                    }
                    else
                    {
                        string text = string.Join('\n', res);
                        if (text.Length > 4096)
                        {
                            preview.buffer.SetText("Too big result [>4KB]");
                        }
                        else
                        {
                            preview.buffer.SetText(text);
                        }
                    }
                });
                thread.Start();
            }
        }


        public override bool HandleEvent(EventBase e)
        {
            switch (e)
            {
                case QuitEvent:
                    Environment.Exit(1);
                    return false;
            }
            bool res = editor.Event(e);
            if (editor.IsDeleted)
            {
                DeleteSelf();
            }
            return res;
        }
    }
}
