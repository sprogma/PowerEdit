using EditorFramework.Widgets;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace SDL2Interface
{
    internal class Render
    {
        static public int W, H;
        protected TextBufferRenderer textRenderer;
        Renderer renderer;
        Window SDLWindow;

        public Render(TextBufferRenderer textRenderer, Renderer renderer, Window sDLWindow)
        {
            this.textRenderer = textRenderer;
            this.renderer = renderer;
            SDLWindow = sDLWindow;
        }


        void ResizeRecurse(BaseWindow window, EditorFramework.Layout.Rect NewPosition)
        {
            // resize this window
            window.Position = NewPosition;
            window.Popup?.Resize(NewPosition);

            // handle special layouts:
            
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
                Rect position = new((int)window.Position.X, (int)window.Position.Y, (int)window.Position.W, (int)window.Position.H);
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
                                    Rect rect = new(position.X + 40, y, position.Width - 80, textRenderer.FontLineStep);
                                    SDL.RenderFillRect(renderer, ref rect);
                                }
                                textRenderer.DrawTextLine(position.X + 50, y, text, 0, [], ref dummyValue);
                                y += textRenderer.FontLineStep;
                            }
                        }
                        break;
                    case FileTabsWindow tabsWindow:
                        {
                            const int tabWidth = 80;
                            const int tabHeight = 25;

                            Rect header = new(position.X, position.Y, position.Width, tabHeight);
                            SDL.SetRenderDrawColor(renderer, 0, 25, 40, 255);
                            SDL.RenderFillRect(renderer, ref header);
                            Rect tab = new(position.X + 2, position.Y + 2, tabWidth - 4, tabHeight - 4);
                            SDL.RenderGetClipRect(renderer, out Rect clip);
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
                                tabsWindow.childs[tabsWindow.current].Draw();
                            }
                        }
                        break;
                }

                window.AfterDraw();
            }

            // after
            unsafe
            {
                SDL.RenderSetClipRect(renderer, null);
            }
        }

        void Draw(BaseWindow window)
        {
            SDL.GetWindowSize(SDLWindow, out W, out H);
            DrawRecurse(window);
        }
    }
}
