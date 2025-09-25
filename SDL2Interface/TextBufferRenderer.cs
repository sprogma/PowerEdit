using RegexTokenizer;
using SDL_Sharp;
using SDL_Sharp.Ttf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL2Interface
{
    internal class TextBufferRenderer
    {
        internal Renderer renderer;
        internal int fontStep;
        internal int fontLineStep;
        internal Font font;
        internal Rect[] asciiMapRectangles;
        internal Texture asciiMap;
        internal ColorTheme colorTheme;

        public TextBufferRenderer(Renderer input_renderer, ColorTheme color_theme)
        {
            colorTheme = color_theme;
            renderer = input_renderer;
            asciiMapRectangles = new Rect[128];
            font = TTF.OpenFont(@"D:\cs\PowerEdit\CascadiaMono.ttf", 32);
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
                asciiMapRectangles[i].Y = 0;
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
            if (asciiMap.IsNull)
            {
                throw new Exception($"Temporary texture is null: {SDL.GetError()}");
            }
            SDL.FreeSurface(textMap);
        }

        public long DrawTextLine(int x, int y, Rope.Rope<char> line, long position, List<Token> tokens, long lastToken)
        {
            foreach (char c in line)
            {
                Token? currentToken = null;
                while (lastToken < tokens.Count && tokens[(int)lastToken].end < position)
                {
                    lastToken++;
                }
                if (lastToken < tokens.Count && tokens[(int)lastToken].begin <= position)
                {
                    currentToken = tokens[(int)lastToken];
                }


                Color color = new(255, 255, 255, 255);
                if (currentToken != null)
                {
                    color = ColorTheme.GetColor(currentToken.Value.type); 
                }

                /* render glyph */
                if (c < ' ' && c != '\n')
                {
                    Rect r = asciiMapRectangles[(byte)c];
                    r.X = x;
                    r.Y = y;
                    r.Width = fontStep;
                    r.Height = fontLineStep;
                    SDL.SetRenderDrawColor(renderer, color.R, color.G, color.B, color.A);
                    SDL.RenderDrawRect(renderer, ref r);
                }
                else if (c < 128)
                {
                    Rect r = asciiMapRectangles[(byte)c];
                    r.X = x;
                    r.Y = y;
                    SDL.SetTextureColorMod(asciiMap, color.R, color.G, color.B);
                    SDL.RenderCopy(renderer, asciiMap, ref asciiMapRectangles[(byte)c], ref r);
                }
                else
                {
                    TTF.SizeText(font, new string(c, 1), out int w, out int h);
                    TTF.RenderText_Blended(font, new string(c, 1), color, out PSurface glythMap);
                    Texture temp = SDL.CreateTextureFromSurface(renderer, glythMap);
                    if (temp.IsNull)
                    {
                        throw new Exception($"Temporary texture is null: {SDL.GetError()}");
                    }
                    Rect src = new(0, 0, w, h);
                    Rect dest = new(x, y, w, h);
                    SDL.RenderCopy(renderer, temp, ref src, ref dest);
                    SDL.FreeSurface(glythMap);
                    SDL.DestroyTexture(temp);
                }
                x += fontStep;
                position++;
            }
            return lastToken;
        }
    }
}
