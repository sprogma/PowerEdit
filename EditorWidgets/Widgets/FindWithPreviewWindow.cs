using EditorCore.Buffer;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using System;
using System.Collections.Generic;
using System.Text;
using TextBuffer;

namespace EditorFramework.Widgets
{
    public class FindWithPreviewWindow : BaseWindow
    {
        public FindWindow find;
        public InputTextWindow preview;
        public bool moditifed;

        public FindWithPreviewWindow(IApplication app, ILayoutManager layout, FindWindow editor) : base(app, layout)
        {
            editor.buffer.ActionOnUpdate += buf => { moditifed = true; };
            this.find = editor;
            this.preview = new(app, 
                               GetLayout<SimpleTextWindow>.Value, 
                               new EditorBuffer(editor.buffer.Server, 
                                                editor.usingCursor.Buffer.Tokenizer, 
                                                null, 
                                                editor.usingCursor.Buffer.LanguageId(), 
                                                editor.usingCursor.Buffer.Text) { TryUseLSP = false }
                               );
        }

        void UpdatePreview()
        {
            find.UpdateResult();

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
