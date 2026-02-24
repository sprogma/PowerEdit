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
        public long viewOffset = 0;
        public bool showNumbers = true;

        public SimpleTextWindow(EditorBuffer buffer, Rect position) : base(position)
        {
            this.buffer = buffer;
        }

        public void SimpleTextWindowDrawText(int leftBarSize)
        {
            long lastToken = 0;
            for (int t = 0; t < H / textRenderer.FontLineStep; ++t)
            {
                int i = t + (int)viewOffset;
                (long index, string? s, _) = buffer.GetLine(i);
                if (s != null)
                {
                    textRenderer.DrawTextLine(leftBarSize + position.X + 5, position.Y + t * textRenderer.FontLineStep, s, index, buffer.Tokens, ref lastToken);
                }
            }
        }

        public void SimpleTextWindowDrawSimpleNumbers(ref int leftBarSize)
        {
            int maxPower = 4;
            long dummyValue = 0;
            /* draw numbers */
            for (int t = 0; t < H / textRenderer.FontLineStep; ++t)
            {
                int i = t + (int)viewOffset;
                (long index, string? s, _) = buffer.GetLine(i);
                if (s != null)
                {
                    int num = i;
                    textRenderer.DrawTextLine(position.X + 5, position.Y + t * textRenderer.FontLineStep, num.ToString().PadLeft(maxPower), 0, [], ref dummyValue);
                }
            }
            leftBarSize = (int)((maxPower + 0.5) * textRenderer.FontStep);
        }

        public override void DrawElements()
        {
            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL.RenderFillRect(renderer, ref position);

            int leftBarSize = 0;

            if (showNumbers)
            {
                SimpleTextWindowDrawSimpleNumbers(ref leftBarSize);
            }
            SimpleTextWindowDrawText(leftBarSize);
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
