using EditorFramework.Layout;
using EditorFramework.Widgets;
using Humanizer;
using Markdig.Helpers;
using Microsoft.CodeAnalysis.Operations;
using SDL_Sharp;
using SDL_Sharp.Utility;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Xml.Linq;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;

namespace SDL2Interface
{
    internal class BaseLayout(Render render) : ILayoutManager
    {
        public Render Render = render;

        public EditorFramework.Layout.Rect Position { get; set; }

        public long? PageStepSize => (Render.textRenderer.Ready ? Render.H / Render.textRenderer.FontLineStep + 1 : 0);

        public virtual void ResizeInternal(BaseWindow window, EditorFramework.Layout.Rect NewSize)
        {
            Position = NewSize;
        }

        public void Resize(BaseWindow window, EditorFramework.Layout.Rect NewSize)
        {
            ResizeInternal(window, NewSize);
            window.Popup?.Layout.Resize(window.Popup, NewSize);
        }

        public void UpdateScale(BaseWindow window, double scale)
        {
            if (Render.textRenderer.Ready)
            {
                Render.textRenderer.Scale(scale);
            }
        }
    }

    internal class HSplitLayout : BaseLayout
    {
        public HSplitLayout(Render render) : base(render) { }

        public override void ResizeInternal(BaseWindow window, EditorFramework.Layout.Rect NewSize)
        {
            base.ResizeInternal(window, NewSize);

            EditorFramework.Layout.Rect lrect = new(NewSize.X, NewSize.Y, NewSize.W / 2, NewSize.H);
            EditorFramework.Layout.Rect rrect = new(NewSize.X + lrect.W, NewSize.Y, NewSize.W - lrect.W, NewSize.H);

            if (window is TreeWalkWithPreviewWindow t)
            {
                t.preview.Layout.Resize(t.preview, lrect);
                t.tree.Layout.Resize(t.tree, rrect);
            }
            else if (window is PowerEditWithPreviewWindow p)
            {
                p.preview.Layout.Resize(p.preview, lrect);
                p.editor.Layout.Resize(p.editor, rrect);
            }
            else
            {
                throw new InvalidOperationException("Give bad window class for HSplit layout.");
            }
        }
    }

    internal class TabLayout : BaseLayout
    {
        public TabLayout(Render render) : base(render) { }

        public override void ResizeInternal(BaseWindow window, EditorFramework.Layout.Rect NewSize)
        {
            base.ResizeInternal(window, NewSize);

            long line = (Render.textRenderer.Ready ? Render.textRenderer.FontLineStep : 0);
            EditorFramework.Layout.Rect rect = new(NewSize.X, NewSize.Y + line, NewSize.W, NewSize.H - line);

            if (window is FileTabsWindow t)
            {
                foreach (var c in t.childs)
                {
                    c.Layout.Resize(c, rect);
                }
            }
            else
            {
                throw new InvalidOperationException("Give bad window class for TabLayout layout.");
            }
        }
    }

    internal class FullLayout : BaseLayout
    {
        public FullLayout(Render render) : base(render) { }

        public override void ResizeInternal(BaseWindow window, EditorFramework.Layout.Rect NewSize)
        {
            base.ResizeInternal(window, NewSize);

            if (window is ProjectEditorWindow t)
            {
                t.Child.Layout.Resize(t.Child, NewSize);
            }
            else
            {
                throw new InvalidOperationException("Give bad window class for TabLayout layout.");
            }
        }
    }

    internal class Render
    {
        static public int W, H;
        static double Scale;
        internal TextBufferRenderer textRenderer;
        public Renderer renderer;
        public Window SDLWindow;

        public Render(TextBufferRenderer textRenderer, Renderer renderer, Window sDLWindow)
        {
            this.textRenderer = textRenderer;
            this.renderer = renderer;
            SDLWindow = sDLWindow;

            SDL.GetDisplayDPI(0, out var ddpi, out var hdpi, out var vdpi);
            Scale = hdpi / 96.0;
            this.textRenderer.Scale(Scale);

            LayoutRegistry.Register<BaseWindow>(() =>
            {
                return new BaseLayout(this);
            });
            LayoutRegistry.Register<ProjectEditorWindow>(() =>
            {
                return new FullLayout(this);
            });
            LayoutRegistry.Register<TreeWalkWithPreviewWindow>(() =>
            {
                return new HSplitLayout(this);
            });
            LayoutRegistry.Register<PowerEditWithPreviewWindow>(() =>
            {
                return new HSplitLayout(this);
            });
            LayoutRegistry.Register<FileTabsWindow>(() =>
            {
                return new TabLayout(this);
            });
        }


        static SDL_Sharp.Rect Convert(EditorFramework.Layout.Rect rect)
        {
            return new SDL_Sharp.Rect((int)rect.X, (int)rect.Y, (int)rect.W, (int)rect.H);
        }


        void DrawRecurse(BaseWindow window)
        {
            if (window.Popup != null)
            {
                DrawRecurse(window.Popup);
            }
            else
            {
                // prepare
                SDL_Sharp.Rect position = new((int)window.Layout.Position.X, (int)window.Layout.Position.Y, (int)window.Layout.Position.W, (int)window.Layout.Position.H);
                unsafe
                {
                    SDL.RenderSetClipRect(renderer, ref position);
                }

                window.PreDraw();

                // draw window
                switch (window)
                {
                    case AlertWindow alertWindow:
                        {
                            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
                            SDL.RenderFillRect(renderer, ref position);

                            long dummyValue = 0;
                            textRenderer.DrawTextLine(position.X, position.Y, alertWindow.Text, 0, [], ref dummyValue);
                            int y = position.Y + textRenderer.FontLineStep;
                            foreach (var (i, (text, _)) in alertWindow.Buttons.Index())
                            {
                                if (alertWindow.Selected == i)
                                {
                                    SDL.SetRenderDrawColor(renderer, 0, 50, 80, 255);
                                    SDL_Sharp.Rect rect = new((int)(position.X + 40 * Scale), y, (int)(position.Width - 80 * Scale), textRenderer.FontLineStep);
                                    SDL.RenderFillRect(renderer, ref rect);
                                }
                                textRenderer.DrawTextLine((int)(position.X + 50 * Scale), y, text, 0, [], ref dummyValue);
                                y += textRenderer.FontLineStep;
                            }
                        }
                        break;
                    case FileTabsWindow tabsWindow:
                        {
                            int tabWidth = (int)(80 * Scale);
                            int tabHeight = (int)(25 * Scale);

                            SDL_Sharp.Rect header = new(position.X, position.Y, position.Width, tabHeight);
                            SDL.SetRenderDrawColor(renderer, 0, 25, 40, 255);
                            SDL.RenderFillRect(renderer, ref header);
                            SDL_Sharp.Rect tab = new(position.X + 2, position.Y + 2, tabWidth - 4, tabHeight - 4);
                            SDL.RenderGetClipRect(renderer, out SDL_Sharp.Rect clip);
                            foreach (var (id, child) in tabsWindow.childs.Index())
                            {
                                SDL.RenderSetClipRect(renderer, ref tab);
                                if (tabsWindow.current == id)
                                {
                                    SDL.SetRenderDrawColor(renderer, 0, 50, 80, 255);
                                    SDL.RenderFillRect(renderer, ref tab);
                                }
                                long dummyValue = 0;
                                textRenderer.DrawTextLine(tab.X, tab.Y, child.file.filename ?? "<Unnamed>", 0, [], ref dummyValue);
                                tab.X += tab.Width + 4;
                            }
                            SDL.RenderSetClipRect(renderer, ref clip);
                            if (tabsWindow.current < tabsWindow.childs.Count)
                            {
                                DrawRecurse(tabsWindow.childs[tabsWindow.current]);
                            }
                        }
                        break;
                    case FileEditorWindow file:
                        DrawInputTextFunction(file);
                        break;
                    case InputTextWindow textWindow:
                        DrawInputTextFunction(textWindow);
                        return;
                    case PowerEditWithPreviewWindow edit:
                        DrawRecurse(edit.editor);
                        DrawRecurse(edit.preview);
                        break;
                    case TreeWalkWithPreviewWindow tree:
                        DrawRecurse(tree.tree);
                        lock (tree.previewLock)
                        {
                            DrawRecurse(tree.preview);
                        }
                        break;
                    case SimpleTextWindow text:
                        DrawSimpleWindow(text);
                        break;
                    case TreeWalkWindow tree:
                        DrawTreeView(tree);
                        break;
                    case ProjectEditorWindow win:
                        DrawRecurse(win.Child);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                window.AfterDraw();
            }

            // after
            unsafe
            {
                SDL.RenderSetClipRect(renderer, null);
            }
        }


        private void DrawInputTextFunction(InputTextWindow window)
        {
            SDL_Sharp.Rect Position = Convert(window.Layout.Position);
            {
                if (window.cursor == null || !textRenderer.fontSizeLoaded)
                {
                    return;
                }
                /* align offset to see cursor */
                if (window.cursor.Selections.Count > 0)
                {
                    long cursorLine = window.cursor.Selections[0].EndLine;
                    if (cursorLine < window.viewOffset + 3)
                    {
                        window.viewOffset = cursorLine - 3;
                    }
                    if (cursorLine > window.viewOffset + window.Layout.Position.H / textRenderer.FontLineStep - 4)
                    {
                        window.viewOffset = cursorLine - window.Layout.Position.H / textRenderer.FontLineStep + 4;
                    }
                }
            }



            if (!textRenderer.Ready) return;

            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL.RenderFillRect(renderer, ref Position);
            long minLine = window.viewOffset;
            long maxLine = minLine + (window.Layout.Position.H / textRenderer.FontLineStep) + 1;
            long minPos = window.buffer.GetLineOffsets(minLine).begin;
            long maxPos = window.buffer.GetLineOffsets(maxLine).begin;
            maxPos = (maxPos == 0 ? window.buffer.Text.Length + 1 : maxPos + (window.Layout.Position.W / textRenderer.FontStep) + 1);

            int leftBarSize = 0;

            if (window.showNumbers)
            {
                if (window.relativeNumbers && window.cursor?.Selections.Count == 1)
                {
                    long cursorLine = window.cursor.Selections[0].EndLine;
                    int maxPower = 4;
                    long dummyValue = 0;
                    /* draw numbers */
                    for (int t = 0; t < window.Layout.Position.H / textRenderer.FontLineStep; ++t)
                    {
                        int i = t + (int)window.viewOffset;
                        (long index, string? s, _) = window.buffer.GetLine(i, 1);
                        if (s != null)
                        {
                            long num = i;
                            if (num < cursorLine)
                            {
                                num = 100 - (cursorLine - num);
                            }
                            else
                            {
                                num = num - cursorLine;
                            }
                            if (num == 0)
                            {
                                SDL_Sharp.Rect rect = new(Position.X + 5, Position.Y + t * textRenderer.FontLineStep, Position.Width - 10, textRenderer.FontLineStep);
                                SDL.SetRenderDrawColor(renderer, 0, 20, 20, 255);
                                SDL.RenderFillRect(renderer, ref rect);
                            }
                            else
                            {
                                textRenderer.DrawTextLine(Position.X + 5, Position.Y + t * textRenderer.FontLineStep, num.ToString().PadLeft(maxPower), 0, [], ref dummyValue);
                            }
                        }
                    }
                    leftBarSize = (int)((maxPower + 0.5) * textRenderer.FontStep);
                }
                else
                {
                    SimpleTextWindowDrawSimpleNumbers(window, ref leftBarSize);
                }
            }

            /* find current error */
            string? message = null;
            (long Begin, long End) errorPosition = (0, 0);
            if (window.cursor?.Selections.Count == 1)
            {
                var selection = window.cursor?.Selections[0]!;
                (long line, _) = window.buffer.GetPositionOffsets(selection.End);
                (long begin, long length) = window.buffer.GetLineOffsets(line);
                long end = begin + length;
                lock (window.buffer.ErrorMarksLock)
                {
                    long mindiff = long.MaxValue;
                    for (int i = 0; i < window.buffer.ErrorMarks.Count; ++i)
                    {
                        if (begin <= window.buffer.ErrorMarks[i].End && window.buffer.ErrorMarks[i].Begin < end)
                        {
                            long diff = Math.Abs(window.buffer.ErrorMarks[i].Middle - selection.End);
                            if (diff < mindiff)
                            {
                                mindiff = diff;
                                message = window.buffer.ErrorMarks[i].Message;
                                errorPosition = (window.buffer.ErrorMarks[i].Begin, window.buffer.ErrorMarks[i].End);
                            }
                        }
                    }
                }
            }

            SimpleTextWindowDrawText(window, leftBarSize);

            /* underline error */
            if (message != null)
            {
                SDL.SetRenderDrawColor(renderer, 255, 0, 0, 255);
                int selectionWidth = (int)(6 * textRenderer.currentScale);
                FillLinesFromTo(window, leftBarSize, selectionWidth, minPos, maxPos, minLine, maxLine, errorPosition.Begin, errorPosition.End);
            }

            /* draw cursor */
            if (window.cursor != null)
            {
                SDL.SetRenderDrawColor(renderer, 255, 255, 255, 255);
                int selectionWidth = (int)(5 * textRenderer.currentScale);

                foreach (var selection in window.cursor.Selections)
                {
                    /* draw vericall line */
                    if (minPos <= selection.End && selection.End < maxPos)
                    {
                        (long line, long offset) = window.buffer.GetPositionOffsets(selection.End);
                        SDL_Sharp.Rect r = new(leftBarSize + Position.X + 5 + (int)offset * textRenderer.FontStep, Position.Y + (int)(line - window.viewOffset) * textRenderer.FontLineStep, 5, textRenderer.FontLineStep);
                        SDL.RenderFillRect(renderer, ref r);
                    }

                    FillLinesFromTo(window, leftBarSize, selectionWidth, minPos, maxPos, minLine, maxLine, selection.Min, selection.Max);
                }
            }

            /* draw current error */
            if (message != null)
            {
                textRenderer.Scale(0.8);
                textRenderer.DrawTextLine(Position.X + textRenderer.FontStep * 25, Position.Y + Position.Height - 5 - textRenderer.FontLineStep, message, 0, new(255, 0, 0, 255));
                textRenderer.Scale(1.25);
            }
        }

        private void FillLinesFromTo(SimpleTextWindow window, int leftBarSize, int selectionWidth, long minPos, long maxPos, long minLine, long maxLine, long from, long to)
        {
            SDL_Sharp.Rect Position = Convert(window.Layout.Position);

            (long line, long offset) begin, end;
            if (from < minPos)
            {
                begin = (minLine, 0);
            }
            else if (from >= maxPos)
            {
                begin = (maxLine + 1, 0);
            }
            else
            {
                begin = window.buffer.GetPositionOffsets(from);
            }
            if (to < minPos)
            {
                end = (minLine, 0);
            }
            else if (to >= maxPos)
            {
                end = (maxLine + 1, 0);
            }
            else
            {
                end = window.buffer.GetPositionOffsets(to);
            }
            long beginLine = Math.Max(begin.line, minLine);
            long endLine = Math.Min(end.line, maxLine);
            for (long line = beginLine; line <= endLine; line++)
            {
                long startOffset = (line == begin.line) ? begin.offset : 0;
                long endOffset = (line == end.line) ? end.offset : window.buffer.Text.GetLineOffsets(line).length;

                int width = (int)(endOffset - startOffset) * textRenderer.FontStep;
                if (width <= 0) continue;
                SDL_Sharp.Rect r = new(
                    leftBarSize + Position.X + 5 + (int)startOffset * textRenderer.FontStep,
                    Position.Y + (int)(line - window.viewOffset) * textRenderer.FontLineStep + textRenderer.FontLineStep - selectionWidth,
                    width,
                    selectionWidth
                );
                SDL.RenderFillRect(renderer, ref r);
            }
        }

        public void Draw(BaseWindow window)
        {
            SDL.GetWindowSize(SDLWindow, out W, out H);
            window.Layout.Resize(window, new(0, 0, W, H));
            DrawRecurse(window);
        }


        public void SimpleTextWindowDrawText(SimpleTextWindow window, int leftBarSize)
        {
            if (!textRenderer.Ready) return;

            long minLine = window.viewOffset;
            long maxLine = minLine + (window.Layout.Position.H / textRenderer.FontLineStep) + 1;
            long minPos = window.buffer.GetLineOffsets(minLine).begin;
            long maxPos = window.buffer.GetLineOffsets(maxLine).begin;
            long totalLength = window.buffer.Text.Length;
            maxPos = (maxPos == 0 ? window.buffer.Text.Length + 1 : maxPos + (window.Layout.Position.W / textRenderer.FontStep) + 1);

            long selectionWidth = (long)(8 * textRenderer.currentScale);
            SDL.SetRenderDrawColor(renderer, 50, 0, 0, 255);
            lock (window.buffer.ErrorMarksLock)
            {
                foreach (ref var err in CollectionsMarshal.AsSpan(window.buffer.ErrorMarks))
                {
                    if (err.End >= totalLength)
                    {
                        err.End = totalLength;
                        if (err.End <= err.Begin)
                        {
                            err.Begin = Math.Max(0, err.End - 1);
                        }
                    }
                    if (err.Begin < 0)
                    {
                        err.Begin = 0;
                        if (err.End <= err.Begin)
                        {
                            err.End = Math.Min(err.Begin + 1, totalLength);
                        }
                    }
                    FillLinesFromTo(window, leftBarSize, textRenderer.FontLineStep, minPos, maxPos, minLine, maxLine, err.Begin, err.End);
                }
                SDL.SetRenderDrawColor(renderer, 255, 0, 0, 255);
                foreach (var err in window.buffer.ErrorMarks)
                {
                    FillLinesFromTo(window, leftBarSize, (int)selectionWidth, minPos, maxPos, minLine, maxLine, err.Begin, err.End);
                }
            }
            long lastToken = 0;
            for (int t = 0; t < window.Layout.Position.H / textRenderer.FontLineStep; ++t)
            {
                int i = t + (int)window.viewOffset;
                (long index, string? s, _) = window.buffer.GetLine(i, (long)(window.Layout.Position.W / textRenderer.FontStep + 1));
                if (s != null)
                {
                    textRenderer.DrawTextLine(leftBarSize + (int)window.Layout.Position.X + 5, (int)window.Layout.Position.Y + t * textRenderer.FontLineStep, s, index, window.buffer.Tokens, ref lastToken);
                }
            }

            // draw errors count
            textRenderer.Scale(0.8);
            textRenderer.DrawTextLine((int)window.Layout.Position.X + 5, (int)window.Layout.Position.Y + (int)window.Layout.Position.H - 5 - textRenderer.FontLineStep, $"{window.buffer.ErrorMarks.Count} errors in file", 0, new(255, 0, 0, 255));
            textRenderer.Scale(1.25);
        }

        public void SimpleTextWindowDrawSimpleNumbers(SimpleTextWindow window, ref int leftBarSize)
        {
            if (!textRenderer.Ready) return;

            int maxPower = 4;
            long dummyValue = 0;
            /* draw numbers */
            for (int t = 0; t < (int)window.Layout.Position.H / textRenderer.FontLineStep; ++t)
            {
                int i = t + (int)window.viewOffset;
                (long index, string? s, _) = window.buffer.GetLine(i, 1);
                if (s != null)
                {
                    int num = i;
                    textRenderer.DrawTextLine((int)window.Layout.Position.X + 5, (int)window.Layout.Position.Y + t * textRenderer.FontLineStep, num.ToString().PadLeft(maxPower), 0, [], ref dummyValue);
                }
            }
            leftBarSize = (int)((maxPower + 0.5) * textRenderer.FontStep);
        }

        public void DrawTreeView(TreeWalkWindow window)
        {
            const float nodeWidth = 2.0f;
            const float nodeHeight = 1.0f;
            const float nodeXStep = 3.0f;
            const float nodeYStep = 1.3f;
            const float movingSmooth = 10.0f;

            Vector2 positionScale = new(nodeXStep, nodeYStep);

            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL_Sharp.Rect Position = Convert(window.Layout.Position);
            SDL.RenderFillRect(renderer, ref Position);

            /* draw tree, relative to current version */
            foreach (var node in window.tree.Values.Where(x => !x.hidden))
            {
                SDL.SetRenderDrawColor(renderer, 255, 0, 0, 0);
                Vector2 pos = (node.position * positionScale - window.Camera) * window.Scale + new Vector2(Position.Width * 0.5f, Position.Height * 0.5f);
                float w, h;
                w = nodeWidth * window.Scale;
                h = nodeHeight * window.Scale;
                pos -= 0.5f * new Vector2(w, h);
                SDL_Sharp.Rect rect = new() { X = Position.X + (int)pos.X, Y = Position.Y + (int)pos.Y, Width = (int)w, Height = (int)h };
                if (node == window.current)
                {
                    SDL.RenderFillRect(renderer, ref rect);
                }
                else
                {
                    long ww = Math.Min(window.Layout.Position.W, window.Layout.Position.H) / 500;
                    for (long i = 0; i < ww; i++)
                    {
                        SDL.RenderDrawRect(renderer, ref rect);
                        rect.X++;
                        rect.Y++;
                        rect.Width-=2;
                        rect.Height-=2;
                    }
                }
                pos += 0.5f * new Vector2(w, h);
                rect = new() { X = Position.X + (int)pos.X, Y = Position.Y + (int)pos.Y, Width = (int)w, Height = (int)h };
                foreach (var next in new[] { node.up, node.right }.OfType<TreeWalkWindow.Node>())
                {
                    Vector2 nextPos = (next.position * positionScale - window.Camera) * window.Scale + new Vector2(Position.Width * 0.5f, Position.Height * 0.5f);
                    int x = Position.X + (int)nextPos.X, y = Position.Y + (int)nextPos.Y;
                    SDL.RenderDrawLine(renderer, rect.X, rect.Y, x, y);
                }
            }

            {
                float t = 1.0f / (movingSmooth + 1.0f);
                window.Camera = window.Camera * (1.0f - t) + window.current.position * positionScale * t;
                window.Scale = window.Scale * (1.0f - t) + window.DestinationScale * t;
            }
        }
        public void DrawSimpleWindow(SimpleTextWindow window)
        {
            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL_Sharp.Rect rect = Convert(window.Layout.Position);
            SDL.RenderFillRect(renderer, ref rect);

            int leftBarSize = 0;

            if (window.showNumbers)
            {
                SimpleTextWindowDrawSimpleNumbers(window, ref leftBarSize);
            }
            SimpleTextWindowDrawText(window, leftBarSize);
        }
    }
}
