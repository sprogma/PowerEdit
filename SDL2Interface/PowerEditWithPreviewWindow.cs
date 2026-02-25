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

namespace SDL2Interface
{
    internal class PowerEditWithPreviewWindow : BaseWindow
    {
        PowerEditWindow editor;
        SimpleTextWindow preview;
        DateTime lastDrawTime;
        bool moditifed;

        public PowerEditWithPreviewWindow(Rect position, PowerEditWindow editor) : base(position)
        {
            Console.WriteLine("Creating");
            moditifed = true;
            Rect right_position = position;
            Rect left_position = position;
            left_position.Width = position.Width / 2;
            right_position.Width = position.Width - left_position.Width;
            right_position.X += left_position.Width;
            editor.position = left_position;
            editor.buffer.ActionOnUpdate +=  buf => {moditifed = true; };
            this.editor = editor;
            this.preview = new(new EditorBuffer(editor.buffer.Server, "processing ...", editor.usingCursor.Buffer.Tokenizer, new ReadonlyTextBuffer()), right_position);
            this.lastDrawTime = DateTime.UtcNow;
            Console.WriteLine("Created");
        }

        public override void PreDraw()
        {
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
                case EventType.KeyDown:
                    if (e.Keyboard.Keysym.Scancode == Scancode.Escape)
                    {
                        DeleteSelf();
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.Return && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        editor.Apply();
                        DeleteSelf();
                        return false;
                    }
                    break;
            }
            return editor.HandleEvent(e);
        }
    }
}
