using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace SDL2Interface
{
    internal class AlertWindow : BaseWindow
    {
        private string text;
        private (string text, Action callback)[] buttons;
        private int selected = 0;

        public AlertWindow(string text, Rect position, params (string text, Action callback)[] buttons) : base(position)
        {
            Debug.Assert(buttons.Length > 0);
            this.text = text;
            this.position = position;
            this.buttons = buttons;
        }

        public override void DrawElements()
        {
            long dummyValue = 0;
            textRenderer.DrawTextLine(position.X, position.Y, text, 0, [], ref dummyValue);
            int y = position.Y;
            foreach (var (i, (text, _)) in buttons.Index())
            {
                if (selected == i)
                {
                    SDL.SetRenderDrawColor(renderer, 0, 50, 80, 255);
                    Rect rect = new(position.X + 40, y, position.Width - 80, textRenderer.FontLineStep);
                    SDL.RenderFillRect(renderer, ref rect);
                }
                textRenderer.DrawTextLine(position.X + 50, y, text, 0, [], ref dummyValue);
                y += textRenderer.FontLineStep;
            }
        }
        public override bool HandleEvent(Event e)
        {
            switch (e.Type)
            {
                case EventType.Quit:
                    Environment.Exit(1);
                    return false;
                case EventType.KeyDown:
                    {
                        if (e.Keyboard.Keysym.Scancode == Scancode.Up)
                        {
                            selected += buttons.Length - 1;
                            selected %= buttons.Length;
                            return false;
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Down)
                        {
                            selected += 1;
                            selected %= buttons.Length;
                            return false;
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.Return)
                        {
                            buttons[selected].callback();
                            DeleteSelf();
                            return false;
                        }
                    }
                    break;
            }
            return true;
        }
    }
}
