using EditorCore.Buffer;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL2Interface
{
    internal class TreeWalkWithPreviewWindow: BaseWindow
    {
        TreeWalkWindow tree;
        SimpleTextWindow preview;
        DateTime lastDrawTime;
        bool moditifed;

        public TreeWalkWithPreviewWindow(Rect position, TreeWalkWindow tree) : base(position)
        {
            moditifed = true;
            Rect right_position = position;
            Rect left_position = position;
            left_position.Width = position.Width / 2;
            right_position.Width = position.Width - left_position.Width;
            right_position.X += left_position.Width;
            tree.position = left_position;

            this.tree = tree;
            this.preview = new(new EditorBuffer(tree.buffer.Server, "loading ...", tree.buffer.Tokenizer), right_position);
            this.lastDrawTime = DateTime.UtcNow;
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
                    string? previewString = tree.CurrentPreview();
                    if (previewString == null)
                    {
                        moditifed = true;
                    }
                    else
                    {
                        lock (preview)
                        {
                            preview.buffer.SetText(previewString);
                            Console.WriteLine($"set {previewString}");
                            Console.WriteLine($"result len: {preview.buffer.Text.Length}/{previewString.Length}");
                        }
                    }
                });
                thread.Start();
            }
        }

        public override void DrawElements()
        {
            tree.Draw();
            lock (preview)
            {
                preview.Draw();
            }
        }


        public override bool HandleEvent(Event e)
        {
            switch (e.Type)
            {
                case EventType.Quit:
                    Environment.Exit(1);
                    return false;
                case EventType.KeyDown:
                    moditifed = true;
                    break;
            }
            bool res = tree.HandleEvent(e);
            if (tree.deleted)
            {
                DeleteSelf();
            }
            return res;
        }
    }
}
