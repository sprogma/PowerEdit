using ConsoleInterface;
using cColor = ConsoleInterface.Color;
using EditorFramework;
using EditorFramework.Layout;
using EditorFramework.Widgets;
using Humanizer;
using Markdig.Helpers;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Xml.Linq;

namespace SDL2Interface
{
    internal class BaseLayout(Render render) : ILayoutManager
    {
        public Render Render = render;

        public Rect Position { get; set; }

        public long? PageStepSize => Math.Max(Position.H - 5, 5);

        public virtual void ResizeInternal(BaseWindow window, Rect NewSize)
        {
            Position = NewSize;
        }

        public void Resize(BaseWindow window, Rect NewSize)
        {
            ResizeInternal(window, NewSize);
            window.Popup?.Layout.Resize(window.Popup, NewSize);
        }

        public void UpdateScale(BaseWindow window, double scale)
        {
            /* ignore */
        }
    }

    internal class HSplitLayout : BaseLayout
    {
        public HSplitLayout(Render render) : base(render) { }

        public override void ResizeInternal(BaseWindow window, Rect NewSize)
        {
            base.ResizeInternal(window, NewSize);

            Rect lrect = new(NewSize.X, NewSize.Y, NewSize.W / 2, NewSize.H);
            Rect rrect = new(NewSize.X + lrect.W, NewSize.Y, NewSize.W - lrect.W, NewSize.H);

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

        public override void ResizeInternal(BaseWindow window, Rect NewSize)
        {
            base.ResizeInternal(window, NewSize);

            Rect rect = new(NewSize.X, NewSize.Y + 1, NewSize.W, NewSize.H - 1);

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

        public override void ResizeInternal(BaseWindow window, Rect NewSize)
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
        internal ConsoleCanvas Canvas;
        private ColorTheme colorTheme;

        public Render(ColorTheme colorTheme)
        {
            this.colorTheme = colorTheme;
            this.Canvas = new();

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

        void DrawRecurse(BaseWindow window)
        {
            if (window.Popup != null)
            {
                DrawRecurse(window.Popup);
            }
            else
            {
                // prepare
                Rect position = new((int)window.Layout.Position.X, (int)window.Layout.Position.Y, (int)window.Layout.Position.W, (int)window.Layout.Position.H);
                Canvas.ClipRect = position;

                window.PreDraw();

                // draw window
                switch (window)
                {
                    case AlertWindow alertWindow:
                        {
                            Canvas.FillRect(position, " ", cColor.Default, cColor.Default);
                            Canvas.AddString(position.X, position.Y, alertWindow.Text);
                            long y = position.Y + 1;
                            foreach (var (i, (text, _)) in alertWindow.Buttons.Index())
                            {
                                Canvas.AddString(position.X + 4, y, text);
                                if (alertWindow.Selected == i)
                                {
                                    Canvas.ApplyStyle(new(position.X + 4, y, position.W - 4, 1), new cColor(0, 0, 0), new cColor(0, 255, 255));
                                }
                                y++;
                            }
                        }
                        break;
                    case FileTabsWindow tabsWindow:
                        {
                            int tabWidth = 16;

                            Rect header = new(position.X, position.Y, position.W, 1);
                            Canvas.ApplyStyle(header, null, new cColor(0, 25, 40));
                            Rect tab = new(position.X, position.Y, tabWidth, 1);
                            Rect clip = Canvas.ClipRect;
                            foreach (var (id, child) in tabsWindow.childs.Index())
                            {
                                Canvas.ClipRect = tab;
                                if (tabsWindow.current == id)
                                {
                                    Canvas.ApplyStyle(tab, null, new cColor(0, 50, 80));
                                }
                                Canvas.AddString(tab.X, tab.Y, child.file.filename ?? "<Unnamed>");
                                tab.X += tabWidth;
                            }
                            Canvas.ClipRect = clip;
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
            Canvas.ResetClipRect();
        }

        private void DrawInputTextFunction(InputTextWindow window)
        {
            Rect position = window.Layout.Position;
            {
                if (window.cursor == null)
                {
                    return;
                }
                /* align offset to see cursor */
                if (window.cursor?.Selections.Count > 0)
                {
                    long cursorLine = window.cursor.Selections[0].EndLine;
                    if (cursorLine < window.viewOffset + 3)
                    {
                        window.viewOffset = cursorLine - 3;
                    }
                    if (cursorLine > window.viewOffset + position.H - 4)
                    {
                        window.viewOffset = cursorLine - position.H + 4;
                    }
                }
            }

            Canvas.FillRect(position, " ", cColor.Default, cColor.Default);

            int leftBarSize = 0;
            if (window.showNumbers)
            {
                if (window.relativeNumbers && window.cursor?.Selections.Count == 1)
                {
                    long cursorLine = window.cursor.Selections[0].EndLine;
                    int maxPower = 4;
                    /* draw numbers */
                    for (int t = 0; t < position.H; ++t)
                    {
                        int i = t + (int)window.viewOffset;
                        (long index, string? s, _) = window.buffer.GetLine(i);
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
                                Rect rect = new(position.X + 1, position.Y + t, position.W - 1, 1);
                                Canvas.ApplyStyle(rect, null, new cColor(0, 20, 20));
                            }
                            else
                            {
                                Canvas.AddString(position.X + 1, position.Y + t, num.ToString());
                            }
                        }
                    }
                    leftBarSize = (int)(maxPower + 0.5);
                }
                else
                {
                    SimpleTextWindowDrawSimpleNumbers(window, ref leftBarSize);
                }
            }

            /* find current error */
            string? message = null;
            long errorPosition = 0;
            if (window.cursor?.Selections.Count == 1)
            {
                var selection = window.cursor?.Selections[0]!;
                (long line, _) = window.buffer.GetPositionOffsets(selection.End);
                (long begin, long length) = window.buffer.GetLineOffsets(line);
                long end = begin + length;
                lock (window.buffer.ErrorMarks)
                {
                    long mindiff = long.MaxValue;
                    for (int i = 0; i < window.buffer.ErrorMarks.Count; ++i)
                    {
                        if (begin <= window.buffer.ErrorMarks[i].position && window.buffer.ErrorMarks[i].position < end)
                        {
                            long diff = Math.Abs(window.buffer.ErrorMarks[i].position - selection.End);
                            if (diff < mindiff)
                            {
                                mindiff = diff;
                                message = window.buffer.ErrorMarks[i].message;
                                errorPosition = window.buffer.ErrorMarks[i].position;
                            }
                        }
                    }
                }
            }

            SimpleTextWindowDrawText(window, leftBarSize);

            /* underline error */
            if (message != null)
            {
                (long line, long offset) = window.buffer.GetPositionOffsets(errorPosition);
                int x = (int)(leftBarSize + position.X + 1 + offset - 1);
                int y = (int)(position.Y + line - window.viewOffset);
                Rect r = new(x, y, 3, 1);
                Canvas.ApplyStyle(r, null, new cColor(255, 166, 0));
            }

            /* draw cursor */
            if (window.cursor != null)
            {
                long minLine = window.viewOffset;
                long maxLine = minLine + (position.H) + 1;
                long minPos = window.buffer.GetLineOffsets(minLine).begin;
                long maxPos = window.buffer.GetLineOffsets(maxLine).begin;
                maxPos = (maxPos == 0 ? window.buffer.Text.Length + 1 : maxPos + position.W + 1);

                foreach (var selection in window.cursor.Selections)
                {
                    /* draw vericall line */
                    if (minPos <= selection.End && selection.End < maxPos)
                    {
                        (long line, long offset) = window.buffer.GetPositionOffsets(selection.End);
                        Rect r = new(leftBarSize + position.X + 1 + offset, position.Y + (int)(line - window.viewOffset) * 1, 1, 1);
                        Canvas.ApplyStyle(r, null, new cColor(255, 255, 255));
                    }

                    (long line, long offset) begin, end;
                    if (selection.Min < minPos)
                    {
                        begin = (minLine, 0);
                    }
                    else if (selection.Min >= maxPos)
                    {
                        begin = (maxLine + 1, 0);
                    }
                    else
                    {
                        begin = window.buffer.GetPositionOffsets(selection.Min);
                    }
                    if (selection.Max < minPos)
                    {
                        end = (minLine, 0);
                    }
                    else if (selection.Max >= maxPos)
                    {
                        end = (maxLine + 1, 0);
                    }
                    else
                    {
                        end = window.buffer.GetPositionOffsets(selection.Max);
                    }

                    long beginLine = Math.Max(begin.line, minLine);
                    long endLine = Math.Min(end.line, maxLine);
                    for (long line = beginLine; line <= endLine; line++)
                    {
                        long startOffset = (line == begin.line) ? begin.offset : 0;
                        long endOffset = (line == end.line) ? end.offset : window.buffer.Text.GetLineOffsets(line).length;

                        int width = (int)(endOffset - startOffset);
                        if (width <= 0) continue;
                        Rect r = new(
                            leftBarSize + position.X + 1 + startOffset,
                            position.Y + line - window.viewOffset,
                            width,
                            1
                        );
                        Canvas.ApplyStyle(r, new cColor(0, 0, 0), new cColor(255, 255, 255));
                    }
                }
            }

            /* draw current error */
            if (message != null)
            {
                Canvas.AddString(position.Bx + 5, position.By - 1, $"  {message}", new cColor(255, 0, 0), cColor.Default);
            }
        }

        public void Draw(BaseWindow window)
        {
            Canvas.Clear();
            window.Layout.Resize(window, new(0, 0, Canvas.Width, Canvas.Height));
            DrawRecurse(window);
        }

        public void SimpleTextWindowDrawText(SimpleTextWindow window, int leftBarSize)
        {
            foreach (var err in window.buffer.ErrorMarks)
            {
                (long line, long col) = window.buffer.GetPositionOffsets(err.position);
                long y = window.Layout.Position.Y + line - window.viewOffset;
                long x = window.Layout.Position.X + 1 + leftBarSize + col - 1;
                Rect r = new(x, y, 3, 1);
                Canvas.ApplyStyle(r, null, new cColor(80, 0, 0));
            }
            for (int t = 0; t < window.Layout.Position.H; ++t)
            {
                int i = t + (int)window.viewOffset;
                (long index, string? s, _) = window.buffer.GetLine(i);
                if (s != null)
                {
                    // TODO: syntax hilighiting
                    s = s.TrimEnd('\n');
                    Canvas.AddString(leftBarSize + window.Layout.Position.X + 1, window.Layout.Position.Y + t, s);
                }
            }
            // draw errors count
            Canvas.AddString(window.Layout.Position.X, window.Layout.Position.By - 2, $"  {window.buffer.ErrorMarks.Count} errors in file", new cColor(255, 0, 0), cColor.Default);
        }

        public void SimpleTextWindowDrawSimpleNumbers(SimpleTextWindow window, ref int leftBarSize)
        {
            int maxPower = 4;
            /* draw numbers */
            for (int t = 0; t < window.Layout.Position.H; ++t)
            {
                int i = t + (int)window.viewOffset;
                (long index, string? s, _) = window.buffer.GetLine(i);
                if (s != null)
                {
                    int num = i;
                    Canvas.AddString(window.Layout.Position.X + 1, window.Layout.Position.Y + t, num.ToString().PadLeft(maxPower), cColor.Default, cColor.Default);
                }
            }
            leftBarSize = maxPower + 1;
        }

        public void DrawTreeView(TreeWalkWindow window)
        {
            Canvas.FillRect(window.Layout.Position, " ", cColor.Default, cColor.Default);

            Rect position = window.Layout.Position;

            /* draw all lines */
            long w = (long)(TreeWalkWindow.NodeWidth * window.Scale / 10.0),
                 h = (long)(TreeWalkWindow.NodeHeight * window.Scale / 10.0);

            foreach (var node in window.tree.Values.Where(x => !x.hidden))
            {
                Vector2 pos = (node.position - window.Camera) * window.Scale + new Vector2(position.W * 0.5f, position.H * 0.5f);
                long ix = position.X + (long)Math.Round(pos.X / 10.0), iy = position.Y + (long)Math.Round(pos.Y / 10.0);
                foreach (var next in new[] { node.up, node.right }.OfType<TreeWalkWindow.Node>())
                {
                    Vector2 nextPos = (next.position - window.Camera) * window.Scale + new Vector2(position.W * 0.5f, position.H * 0.5f);
                    long x = position.X + (long)Math.Round(nextPos.X / 10.0), y = position.Y + (long)Math.Round(nextPos.Y / 10.0);

                    while (x != ix || y != iy)
                    {
                        if (x < ix) x++;
                        if (x > ix) x--;
                        if (y < iy) y++;
                        if (y > iy) y--;
                        Canvas.SetCell(x, y, "*", new cColor(128, 0, 0), cColor.Default);
                    }
                }
            }

            foreach (var node in window.tree.Values.Where(x => !x.hidden))
            {
                Vector2 pos = (node.position - window.Camera) * window.Scale + new Vector2(position.W * 0.5f, position.H * 0.5f);
                pos -= 0.5f * new Vector2(w, h);
                long ix = position.X + (long)Math.Round(pos.X / 10.0), iy = position.Y + (long)Math.Round(pos.Y / 10.0);
                if (node == window.current)
                {
                    Canvas.AddString(ix, iy, $"#{node.id}", new cColor(0, 0, 0), new cColor(255, 0, 0));
                }
                else
                {
                    Canvas.AddString(ix, iy, $"#{node.id}", new cColor(255, 0, 0), new cColor(60, 0, 0));
                }
            }
            {
                float t = 1.0f / (TreeWalkWindow.MovingSmooth + 1.0f);
                window.Camera = window.Camera * (1.0f - t) + window.current.position * t;
                window.Scale = window.Scale * (1.0f - t) + window.DestinationScale * t;
            }
        }
        public void DrawSimpleWindow(SimpleTextWindow window)
        {
            Canvas.FillRect(window.Layout.Position, " ", cColor.Default, cColor.Default);

            int leftBarSize = 0;

            if (window.showNumbers)
            {
                SimpleTextWindowDrawSimpleNumbers(window, ref leftBarSize);
            }
            SimpleTextWindowDrawText(window, leftBarSize);
        }
    }
}
