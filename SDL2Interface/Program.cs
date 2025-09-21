using EditorCore.Selection;
using SDL_Sharp;
using SDL_Sharp.Ttf;
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
        int fontStep;
        int fontLineStep;
        Font font;
        Rect[] asciiMapRectangles;
        Texture asciiMap;

        public SDL2Interface()
        {
            commandProvider = new PowershellProvider.PowershellProvider();
            server = new EditorCore.Server.EditorServer(commandProvider);
            file = server.OpenFile(@"D:\a.c");
            if (file == null)
            {
                throw new Exception("File not found");
            }
            cursor = file?.CreateCursor();
            cursor?.Selections.Add(new EditorSelection(cursor, 1, 4));


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

            CreateAsciiMap();
        }

        private void CreateAsciiMap()
        {
            asciiMapRectangles = new Rect[128];
            font = TTF.OpenFont(@"D:\cs\PowerEdit\sans.ttf", 32);
            if (font.IsNull)
            {
                throw new Exception("Font is not loaded");
            }
            /* generate rectangles */
            int x = 0;
            for (int i = 32; i < 128; ++i)
            {
                string st = ((char)i).ToString();
                if (TTF.SizeText(font, st, out int w, out int h) != 0)
                {
                    throw new Exception("SizeText exception");
                }
                asciiMapRectangles[i].X = x;
                asciiMapRectangles[i].Y = 0 ;
                asciiMapRectangles[i].Width = w;
                asciiMapRectangles[i].Height = h;
                x += w;
                fontStep = Math.Max(fontStep, w);
                fontLineStep = Math.Max(fontLineStep, h);
            }
            fontLineStep += 3;
            /* render glyphs */
            SDL.CreateRGBSurface(0, x, asciiMapRectangles[127].Height, 32, 0xff, 0xff00, 0xff0000, 0xff000000, out PSurface textMap);
            x = 0;
            for (int i = 32; i < 128; ++i)
            {
                string st = ((char)i).ToString();
                if (TTF.SizeText(font, st, out int w, out int h) != 0)
                {
                    throw new Exception("SizeText exception");
                }
                TTF.RenderText_Blended(font, st, new Color(255, 255, 255, 255), out PSurface glythMap);
                Rect r = new(0, 0, asciiMapRectangles[i].Width, asciiMapRectangles[i].Height);
                SDL.BlitSurface(glythMap, ref r, textMap, ref asciiMapRectangles[i]);
                SDL.FreeSurface(glythMap);
                x += w;
            }
            asciiMap = SDL.CreateTextureFromSurface(renderer, textMap);
            SDL.FreeSurface(textMap);
        }

        private void DrawTextLine(int x, int y, Rope.Rope<char> line)
        {
            foreach (char c in line)
            {
                /* render glyph */
                if (c < ' ' && c != '\n')
                {
                    Rect r = asciiMapRectangles[(byte)c];
                    r.X = x;
                    r.Y = y;
                    r.Width = fontStep;
                    r.Height = fontLineStep;
                    SDL.SetRenderDrawColor(renderer, 255, 255, 255, 255);
                    SDL.RenderDrawRect(renderer, ref r);
                    x += fontStep;
                }
                else
                {
                    Rect r = asciiMapRectangles[(byte)c];
                    r.X = x; 
                    r.Y = y;
                    SDL.RenderCopy(renderer, asciiMap, ref asciiMapRectangles[(byte)c], ref r);
                    x += fontStep;
                }
            }
        }

        private void Draw()
        {
            SDL.GetWindowSize(window, out W, out H);

            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL.RenderClear(renderer);

            /* draw text */
            if (file != null)
            {
                for (int i = 0; i < H / fontLineStep; ++i)
                {
                    Rope.Rope<char>? s = file.GetLine(i);
                    if (s != null)
                    {
                        DrawTextLine(5, i * fontLineStep, s.Value);
                    }
                }

                /* draw cursor */
                if (cursor != null)
                {
                    SDL.SetRenderDrawColor(renderer, 255, 255, 255, 255);
                    foreach (var selection in cursor.Selections)
                    {
                        {
                            (long line, long offset) = file.GetPositionOffsets(selection.End);
                            Rect r = new((int)offset * fontStep, (int)line * fontLineStep, 5, fontLineStep);
                            SDL.RenderFillRect(renderer, ref r);
                        }
                        for (long p = selection.Min; p < selection.Max; ++p)
                        {
                            (long line, long offset) = file.GetPositionOffsets(p);
                            Rect r = new((int)offset * fontStep, (int)line * fontLineStep + fontLineStep - 5, fontStep, 5);
                            SDL.RenderFillRect(renderer, ref r);
                        }
                    }
                }
            }


            SDL.RenderPresent(renderer);
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
                    case EventType.KeyDown:
                        if (e.Keyboard.Keysym.Scancode == Scancode.Right)
                        {
                            cursor?.Selections.ForEach(x => { x.MoveHorisontal(1); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Left)
                        {
                            cursor?.Selections.ForEach(x => { x.MoveHorisontal(-1); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Down)
                        {
                            cursor?.Selections.ForEach(x => { x.MoveVertical(1); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Up)
                        {
                            cursor?.Selections.ForEach(x => { x.MoveVertical(-1); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Backspace)
                        {
                            cursor?.Selections.ForEach(x => { x.Cursor.File.DeleteString(x.Begin - 1, 1); });
                        }
                        if (e.Keyboard.Keysym.Scancode >= Scancode.A &&
                            e.Keyboard.Keysym.Scancode <= Scancode.Z)
                        {
                            cursor?.Selections.ForEach(x => x.InsertText(new string((char)('a' + e.Keyboard.Keysym.Scancode - Scancode.A), 1)));
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
                Thread.Sleep(100);
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
