using EditorCore.Buffer;
using EditorCore.Cursor;
using EditorCore.Selection;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL2Interface
{
    internal class SimpleTextWindow : BaseWindow
    {
        internal EditorBuffer buffer;

        public SimpleTextWindow(EditorBuffer buffer, Rect position) : base(position)
        {
            this.buffer = buffer;
        }

        public override void DrawElements()
        {
            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL.RenderFillRect(renderer, ref position);

            long lastToken = 0;

            /* draw text */
            for (int i = 0; i < H / textRenderer.fontLineStep; ++i)
            {
                (long index, Rope.Rope<char>? s) = buffer.GetLine(i);
                if (s != null)
                {
                    lastToken = textRenderer.DrawTextLine(position.X + 5, position.Y + i * textRenderer.fontLineStep, s.Value, index, buffer.Tokens, lastToken);
                }
            }
        }

        public override bool HandleEvent(Event e)
        {
            switch (e.Type)
            {
                case EventType.Quit:
                    Environment.Exit(1);
                    return false;
            }
            return base.HandleEvent(e);
        }
    }
}
