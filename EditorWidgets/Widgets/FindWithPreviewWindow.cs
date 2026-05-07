using EditorCore.Buffer;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using TextBuffer;

namespace EditorFramework.Widgets
{
    public class FindWithPreviewWindow : BaseWindow
    {
        public FindWindow find;
        public InputTextWindow? preview;
        public bool moditifed;

        public FindWithPreviewWindow(IApplication app, ILayoutManager layout, FindWindow editor) : base(app, layout)
        {
            editor.buffer.ActionOnUpdate += buf => { moditifed = true; };
            this.find = editor;
        }

        void UpdatePreview()
        {
            find.UpdateResult();

            if (preview == null || preview.buffer != find.resultBuffer)
            {
                preview = new(App,
                                   GetLayout<SimpleTextWindow>.Value,
                                   (new EditorBuffer(find.resultBuffer.Server,
                                                    find.resultBuffer.Tokenizer,
                                                    null,
                                                    find.resultBuffer.LanguageId(),
                                                    find.resultBuffer.Text)
                                   { TryUseLSP = false }).Cursor
                                   )
                { relativeNumbers = false };
                if (find.usingCursor.Selections.Count > 0)
                {
                    preview.buffer.Cursor?.Selections.Clear();
                    preview.buffer.Cursor?.Selections.Add(find.usingCursor.Selections[0]);
                }
            }
            if (find.resultBegin != null && find.resultEnd != null)
            {
                preview.cursor?.Selections.Clear();
                preview.cursor?.Selections.Add(new(preview.cursor, find.resultBegin.Value, find.resultEnd.Value));
            }
            else
            {
                preview.cursor?.Selections.Clear();
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
            bool res = find.Event(e);
            if (find.IsDeleted)
            {
                DeleteSelf();
            }
            UpdatePreview();
            return res;
        }
    }
}
