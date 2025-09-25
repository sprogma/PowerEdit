using EditorCore.Buffer;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SDL2Interface
{
    internal class PowerEditPreviewWindow : BaseWindow
    {
        PowerEditWindow editor;
        SimpleTextWindow preview;
        DateTime lastDrawTime;

        public PowerEditPreviewWindow(Rect position, PowerEditWindow editor) : base(position)
        {
            Console.WriteLine("Creating");
            Rect right_position = position;
            Rect left_position = position;
            left_position.Width = position.Width / 2;
            right_position.Width = position.Width - left_position.Width;
            right_position.X += left_position.Width;
            editor.position = left_position;
            this.editor = editor;
            this.preview = new(new EditorBuffer(editor.buffer.Server, "processing ..."), right_position);
            this.lastDrawTime = DateTime.UtcNow;
            Console.WriteLine("Created");
        }

        public override void PreDraw()
        {
            if ((DateTime.UtcNow - lastDrawTime).TotalSeconds > 1)
            {
                lastDrawTime = DateTime.UtcNow;
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
                        preview.buffer.SetText(string.Join('\n', res));
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
