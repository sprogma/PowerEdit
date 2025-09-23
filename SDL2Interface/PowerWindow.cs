using EditorCore.Buffer;
using EditorCore.Cursor;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL2Interface
{
    internal class PowerWindow
    {
        int W, H;
        Window window;
        Renderer renderer;
        TextBufferRenderer textRenderer;
        bool running;
        int FontStep => textRenderer.fontStep;
        int FontLineStep => textRenderer.fontLineStep;
        public EditorCursor OldCursor { get; internal set; }
        public EditorCursor Cursor { get; internal set; }
        public EditorBuffer Command { get; internal set; }


        public PowerWindow(Window wnd, Renderer rnd, TextBufferRenderer TextRenderer, EditorCursor cursor)
        {
            Command = new(cursor.Buffer.Server, "$input | %{}");
            Cursor = Command.CreateCursor();
            Cursor.Selections.Add(new EditorCore.Selection.EditorSelection(Cursor, 11));
            
            running = true;
            textRenderer = TextRenderer;
            window = wnd;
            renderer = rnd;
            OldCursor = cursor;
        }
        private void Draw()
        {
            SDL.GetWindowSize(window, out W, out H);

            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL.RenderClear(renderer);

            /* draw text */
            for (int i = 0; i < H / FontLineStep; ++i)
            {
                Rope.Rope<char>? s = Cursor.Buffer.GetLine(i);
                if (s != null)
                {
                    textRenderer.DrawTextLine(5, i * FontLineStep, s.Value);
                }
            }

            /* draw Cursor */
            if (Cursor != null)
            {
                SDL.SetRenderDrawColor(renderer, 255, 255, 255, 255);
                foreach (var selection in Cursor.Selections)
                {
                    {
                        (long line, long offset) = Cursor.Buffer.GetPositionOffsets(selection.End);
                        Rect r = new((int)offset * FontStep, (int)line * FontLineStep, 5, FontLineStep);
                        SDL.RenderFillRect(renderer, ref r);
                    }
                    for (long p = selection.Min; p < selection.Max; ++p)
                    {
                        (long line, long offset) = Cursor.Buffer.GetPositionOffsets(p);
                        Rect r = new((int)offset * FontStep, (int)line * FontLineStep + FontLineStep - 5, FontStep, 5);
                        SDL.RenderFillRect(renderer, ref r);
                    }
                }
            }

            SDL.RenderPresent(renderer);
        }

        private void Apply()
        {
            OldCursor.ApplyCommand(Command.Text.ToString());
        }

        private unsafe string GetTextInputValue(TextInputEvent e)
        {
            byte* p = e.Text;
            int len = 0;
            while (p[len] != 0) len++;
            return Encoding.UTF8.GetString(p, len);
        }

        private void HandleEvents()
        {
            while (SDL.PollEvent(out Event e) != 0)
            {
                switch (e.Type)
                {
                    case EventType.Quit:
                        Environment.Exit(1);
                        break;
                    case EventType.TextInput:
                        string s = GetTextInputValue(e.Text);
                        /* clear all selection */
                        Cursor?.Selections.ForEach(x => x.Cursor.Buffer.DeleteString(x.Min, x.TextLength));
                        Cursor?.Selections.ForEach(x => x.InsertText(s));
                        break;
                    case EventType.KeyDown:
                        if (e.Keyboard.Keysym.Scancode == Scancode.E && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                        {
                            /* v.1 - open powerWindow */
                            if (Cursor != null)
                            {
                                PowerWindow win = new(window, renderer, textRenderer, Cursor);
                                win.Run();
                            }
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Escape)
                        {
                            running = false;
                            return;
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Return && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                        {
                            Apply();
                            running = false;
                            return;
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Right)
                        {
                            Cursor?.Selections.ForEach(x => { x.MoveHorisontal(1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Left)
                        {
                            Cursor?.Selections.ForEach(x => { x.MoveHorisontal(-1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Down)
                        {
                            Cursor?.Selections.ForEach(x => { x.MoveVertical(1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Up)
                        {
                            Cursor?.Selections.ForEach(x => { x.MoveVertical(-1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Tab)
                        {
                            Cursor?.Selections.ForEach(x =>
                            {
                                if (x.TextLength > 0)
                                {
                                }
                                else
                                {
                                    x.InsertText("    ");
                                }
                            });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Backspace)
                        {
                            Cursor?.Selections.ForEach(x =>
                            {
                                if (x.TextLength == 0)
                                {
                                    x.Cursor.Buffer.DeleteString(x.End - 1, 1);
                                }
                                else
                                {
                                    x.Cursor.Buffer.DeleteString(x.Min, x.TextLength);
                                }
                            });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Return)
                        {
                            Cursor?.Selections.ForEach(x => x.InsertText("\n"));
                        }
                        break;
                }
            }
        }

        public void Run()
        {
            while (running)
            {
                Draw();
                HandleEvents();
                /* comment out to fastest performance */
                Thread.Sleep(10);
            }
        }
    }
}

