using EditorCore.Selection;
using SDL_Sharp;
using SDL_Sharp.Ttf;
using System.Runtime.InteropServices;
using System.Text;

namespace SDL2Interface
{
    class SDL2Interface
    {
        EditorCore.Cursor.EditorCursor? cursor;
        EditorCore.File.EditorFile? file;
        EditorCore.Server.EditorServer server;
        PowershellProvider.PowershellProvider commandProvider;
        int W, H;
        Window window;
        Renderer renderer;
        TextBufferRenderer textRenderer;
        int FontStep => textRenderer.fontStep;
        int FontLineStep => textRenderer.fontLineStep;

        public SDL2Interface()
        {
            commandProvider = new PowershellProvider.PowershellProvider();
            server = new EditorCore.Server.EditorServer(commandProvider);
            file = server.OpenFile(@"D:\a.c");
            if (file == null)
            {
                throw new Exception("File not found");
            }
            cursor = file.Buffer?.CreateCursor();
            cursor?.Selections.Add(new EditorSelection(cursor, 1, 4));
            cursor?.Selections.Add(new EditorSelection(cursor, 6, 8));


            if (SDL.Init(SdlInitFlags.Everything) != 0)
            {
                throw new Exception("SDL initialization failed");
            }
            if (TTF.Init() != 0)
            {
                throw new Exception("SDL TTF initialization failed");
            }
            if (SDL.CreateWindowAndRenderer(1600, 900, 0, out window, out renderer) != 0)
            {
                throw new Exception("SDL initialization failed");
            }
            SDL.GetWindowSize(window, out W, out H);

            textRenderer = new TextBufferRenderer(renderer);
        }

        private void Draw()
        {
            SDL.GetWindowSize(window, out W, out H);

            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL.RenderClear(renderer);

            /* draw text */
            if (file != null)
            {
                for (int i = 0; i < H / FontLineStep; ++i)
                {
                    Rope.Rope<char>? s = file.Buffer.GetLine(i);
                    if (s != null)
                    {
                        textRenderer.DrawTextLine(5, i * FontLineStep, s.Value);
                    }
                }

                /* draw cursor */
                if (cursor != null)
                {
                    SDL.SetRenderDrawColor(renderer, 255, 255, 255, 255);
                    foreach (var selection in cursor.Selections)
                    {
                        {
                            (long line, long offset) = file.Buffer.GetPositionOffsets(selection.End);
                            Rect r = new((int)offset * FontStep, (int)line * FontLineStep, 5, FontLineStep);
                            SDL.RenderFillRect(renderer, ref r);
                        }
                        for (long p = selection.Min; p < selection.Max; ++p)
                        {
                            (long line, long offset) = file.Buffer.GetPositionOffsets(p);
                            Rect r = new((int)offset * FontStep, (int)line * FontLineStep + FontLineStep - 5, FontStep, 5);
                            SDL.RenderFillRect(renderer, ref r);
                        }
                    }
                }
            }


            SDL.RenderPresent(renderer);
        }

        private unsafe string GetTextInputValue(TextInputEvent e) {
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
                        cursor?.Selections.ForEach(x => x.Cursor.Buffer.DeleteString(x.Min, x.TextLength));
                        cursor?.Selections.ForEach(x => x.InsertText(s));
                        break;
                    case EventType.KeyDown:
                        if (e.Keyboard.Keysym.Scancode == Scancode.S && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                        {
                            file?.Save(@"D:\a.c");
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.E && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                        {
                            /* v.1 - open powerWindow */
                            if (cursor != null)
                            {
                                PowerWindow win = new(window, renderer, textRenderer, cursor);
                                win.Run();
                            }
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.C && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0)
                        {
                            if (cursor != null)
                            {
                                if (cursor.Selections.Count > 1)
                                {
                                    cursor.Selections = [cursor.Selections[0]];
                                }
                            }
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Right)
                        {
                            cursor?.Selections.ForEach(x => { x.MoveHorisontal(1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Left)
                        {
                            cursor?.Selections.ForEach(x => { x.MoveHorisontal(-1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Down)
                        {
                            cursor?.Selections.ForEach(x => { x.MoveVertical(1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Up)
                        {
                            cursor?.Selections.ForEach(x => { x.MoveVertical(-1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Tab)
                        {
                            cursor?.Selections.ForEach(x =>
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
                            cursor?.Selections.ForEach(x =>
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
                            cursor?.Selections.ForEach(x => x.InsertText("\n"));
                        }
                        break;
                }
            }
        }

        public void Run()
        {
            while (true)
            {
                Draw();
                HandleEvents();
                /* comment out to fastest performance */
                Thread.Sleep(10);
            }
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            foreach (string s in args)
            {
                Console.WriteLine(s);
            }
            /* create application instance */
            SDL2Interface app = new SDL2Interface();
            app.Run();
        }
    }
}
