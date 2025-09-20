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
            cursor?.Selections.Add(new EditorSelection(cursor, 0));


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
            }
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
                Rect r = asciiMapRectangles[(byte)c];
                r.X = x; 
                r.Y = y;
                SDL.RenderCopy(renderer, asciiMap, ref asciiMapRectangles[(byte)c], ref r);
                x += r.Width;
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
                for (int i = 0; i < H / 50; ++i)
                {
                    Rope.Rope<char>? s = file.GetLine(i);
                    if (s != null)
                    {
                        DrawTextLine(5, i * 50, s.Value);
                    }
                }

                /* draw cursor */
                if (cursor != null)
                {
                    SDL.SetRenderDrawColor(renderer, 255, 255, 255, 255);
                    foreach (var selection in cursor.Selections)
                    {
                        (long line, long offset) = file.GetPositionOffsets(selection.Begin);
                        Rect r = new((int)offset * 20, (int)line * 50, 5, 50);
                        SDL.RenderFillRect(renderer, ref r);
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
                            cursor?.Selections.ForEach(x => { x.SetPosition(x.Begin + 1); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Left)
                        {
                            cursor?.Selections.ForEach(x => { x.SetPosition(x.Begin - 1); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Backspace)
                        {
                            cursor?.Selections.ForEach(x => { x.Cursor.File.DeleteString(x.Begin - 1, 1); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.A)
                        {
                            cursor?.Selections.ForEach(x => { x.InsertText("a"); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.B)
                        {
                            cursor?.Selections.ForEach(x => { x.InsertText("b"); });
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.C)
                        {
                            cursor?.Selections.ForEach(x => { x.InsertText("c"); });
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
