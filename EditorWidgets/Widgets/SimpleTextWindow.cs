using EditorCore.Buffer;
using EditorCore.Cursor;
using EditorCore.Selection;
using EditorFramework.ApplicationApi;
using EditorFramework.Layout;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace EditorFramework.Widgets
{
    public class SimpleTextWindow : BaseWindow
    {
        public EditorBuffer buffer;
        public long viewOffset = 0;
        public bool showNumbers = true;

        public SimpleTextWindow(IApplication App, EditorBuffer buffer, Rect position) : base(App, position)
        {
            this.buffer = buffer;
        }

        public void SimpleTextWindowDrawText(int leftBarSize)
        {
            if (!textRenderer.Ready) return;

            long selectionWidth = (long)(8 * textRenderer.currentScale);
            SDL.SetRenderDrawColor(renderer, 50, 0, 0, 255);
            foreach (var err in buffer.ErrorMarks)
            {
                (long line, long col) = buffer.GetPositionOffsets(err.position);
                long y = Position.Y + (line - viewOffset) * textRenderer.FontLineStep;
                long x = Position.X + 5 + leftBarSize + col * textRenderer.FontStep - textRenderer.FontStep / 2;
                Rect r = new((int)x, (int)y, 2 * textRenderer.FontStep, (int)textRenderer.FontLineStep);
                SDL.RenderFillRect(renderer, ref r);
            }
            SDL.SetRenderDrawColor(renderer, 255, 0, 0, 255);
            foreach (var err in buffer.ErrorMarks)
            {
                (long line, long col) = buffer.GetPositionOffsets(err.position);
                long y = Position.Y + (line - viewOffset + 1) * textRenderer.FontLineStep - selectionWidth;
                long x = Position.X + 5 + leftBarSize + col * textRenderer.FontStep - textRenderer.FontStep / 2;
                Rect r = new((int)x, (int)y, 2 * textRenderer.FontStep, (int)selectionWidth);
                SDL.RenderFillRect(renderer, ref r);
            }
            long lastToken = 0;
            for (int t = 0; t < H / textRenderer.FontLineStep; ++t)
            {
                int i = t + (int)viewOffset;
                (long index, string? s, _) = buffer.GetLine(i);
                if (s != null)
                {
                    textRenderer.DrawTextLine(leftBarSize + Position.X + 5, Position.Y + t * textRenderer.FontLineStep, s, index, buffer.Tokens, ref lastToken);
                }
            }

            // draw errors count
            long dummyValue = 0;
            textRenderer.Scale(0.8);
            textRenderer.DrawTextLine(Position.X + 5, Position.Y + H - 5 - textRenderer.FontLineStep, $"{buffer.ErrorMarks.Count} errors in file", 0, [], ref dummyValue);
            textRenderer.Scale(1.25);
        }

        public void SimpleTextWindowDrawSimpleNumbers(ref int leftBarSize)
        {
            if (!textRenderer.Ready) return;

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
                    textRenderer.DrawTextLine(Position.X + 5, Position.Y + t * textRenderer.FontLineStep, num.ToString().PadLeft(maxPower), 0, [], ref dummyValue);
                }
            }
            leftBarSize = (int)((maxPower + 0.5) * textRenderer.FontStep);
        }

        public override void DrawElements()
        {
            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL.RenderFillRect(renderer, ref Position);

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
