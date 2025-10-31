using EditorCore.Buffer;
using EditorCore.Cursor;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Drawing.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL2Interface
{
    internal abstract class BaseWindow
    {
        static public int W, H;
        static public Window window;
        static public Renderer renderer;
        public TextBufferRenderer textRenderer;
        public bool deleted = false;

        internal Rect position;

        public BaseWindow(Rect position)
        {
            textRenderer = new TextBufferRenderer(renderer, new ColorTheme());
            this.position = position;
        }

        public virtual void PreDraw()
        {
            SDL.GetWindowSize(window, out W, out H);
            SDL.RenderSetClipRect(renderer, ref position);
        }

        unsafe public virtual void AfterDraw()
        {
            SDL.RenderSetClipRect(renderer, null);
        }

        public abstract void DrawElements();

        public virtual void Draw()
        {
            PreDraw();
            DrawElements();
            AfterDraw();
        }

        /// <summary>
        /// Helping function to get TextInputEvent value
        /// </summary>
        /// <param name="e">TextInputEvent value to read</param>
        /// <returns> input string </returns>
        internal unsafe string GetTextInputValue(TextInputEvent e)
        {
            byte* p = e.Text;
            int len = 0;
            while (p[len] != 0) len++;
            return Encoding.UTF8.GetString(p, len);
        }

        /// <summary>
        /// Handles given event
        /// </summary>
        /// <param name="e"> Event to handle </param>
        /// <returns> true if event needs to fall down (to next window in queue) </returns>
        public virtual bool HandleEvent(Event e)
        {
            return true;
        }

        /// <summary>
        /// Used to close this window from itself.
        /// </summary>
        public void DeleteSelf()
        {
            Program.windows.Remove(this);
            this.deleted = true;
        }
    }
}
