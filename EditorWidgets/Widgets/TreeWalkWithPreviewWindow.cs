using EditorCore.Buffer;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;

namespace EditorFramework.Widgets
{
    public class TreeWalkWithPreviewWindow : BaseWindow
    {
        public TreeWalkWindow tree;
        public SimpleTextWindow preview;
        public Lock previewLock = new();
        public DateTime lastDrawTime;
        public bool moditifed;

        public TreeWalkWithPreviewWindow(IApplication app, ILayoutManager layout, TreeWalkWindow tree) : base(app, layout)
        {
            moditifed = true;

            this.tree = tree;
            this.preview = new(app, GetLayout<SimpleTextWindow>.Value, new EditorBuffer(tree.buffer.Server, "loading ...", tree.buffer.Tokenizer, null, "", tree.buffer.LanguageId(), new ReadonlyTextBuffer()));
            this.lastDrawTime = DateTime.UtcNow;
        }

        public override void PreDraw()
        {
            base.PreDraw();
            if ((DateTime.UtcNow - lastDrawTime).TotalSeconds > 0.1 && moditifed)
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
                        lock (previewLock)
                        {
                            preview.buffer.SetText(previewString);
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
                case KeyChordEvent:
                    moditifed = true;
                    break;
            }
            bool res = tree.Event(e);
            if (tree.IsDeleted)
            {
                DeleteSelf();
            }
            return res;
        }
    }
}
