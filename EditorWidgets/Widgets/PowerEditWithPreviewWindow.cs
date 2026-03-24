using EditorCore.Buffer;
using RegexTokenizer;
using SDL_Sharp;
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
    internal class PowerEditWithPreviewWindow : BaseWindow
    {
        PowerEditWindow editor;
        SimpleTextWindow preview;
        DateTime lastDrawTime;
        bool moditifed;

        public PowerEditWithPreviewWindow(Rect position, PowerEditWindow editor) : base(position)
        {
            moditifed = true;
            editor.buffer.ActionOnUpdate +=  buf => {moditifed = true; };
            this.editor = editor;
            this.preview = new(new EditorBuffer(editor.buffer.Server, "processing ...", editor.usingCursor.Buffer.Tokenizer, null, "", new ReadonlyTextBuffer()), new());
            Resize(position);
            this.lastDrawTime = DateTime.UtcNow;
        }

        public override void Resize(Rect newPosition)
        {
            base.Resize(newPosition);
            Rect right_position = Position;
            Rect left_position = Position;
            left_position.Width = Position.Width / 2;
            right_position.Width = Position.Width - left_position.Width;
            right_position.X += left_position.Width;
            editor.Resize(left_position);
            preview.Resize(right_position);
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

        public override void DrawElements()
        {
            editor.Draw();
            preview.Draw();
        }


        public override bool HandleEvent(Event e)
        {
            switch (e.Type)
            {
                case EventType.Quit:
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
