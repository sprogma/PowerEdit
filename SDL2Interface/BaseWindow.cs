using EditorCore.Buffer;
using EditorCore.Cursor;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Drawing.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SDL2Interface
{
    delegate void OnQuitAction(BaseWindow window);

    internal abstract class BaseWindow
    {
        static public int W, H;
        static public Window window;
        static public Renderer renderer;
        public bool deleted = false;

        protected BaseWindow? popup = null;
        protected BaseWindow? parent = null;

        protected TextBufferRenderer textRenderer;
        protected Rect position;

        public OnQuitAction? OnQuit = null;

        public BaseWindow(Rect position)
        {
            textRenderer = new TextBufferRenderer(renderer, new ColorTheme());
            this.position = position;
        }

        public void ReleasePopup()
        {
            popup?.parent = null;
            popup = null;
        }

        public void OpenPopup(BaseWindow window)
        {
            if (popup != null)
            {
                throw new Exception("Open popup while having one");
            }
            popup = window;
            window.parent = this;
        }

        public virtual void Resize(Rect newPosition)
        {
            position = newPosition;
            popup?.Resize(newPosition);
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

        public void Draw()
        {
            if (popup != null)
            {
                popup.Draw();
            }
            else
            {
                PreDraw();
                DrawElements();
                AfterDraw();
            }
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
        /// Helping function to get TextInputEvent value
        /// </summary>
        /// <param name="e">TextInputEvent value to read</param>
        /// <returns> input bytes</returns>
        internal unsafe byte[] GetTextInputBytes(TextInputEvent e)
        {
            byte* p = e.Text;
            if (p == null) return [];
            int len = 0;
            while (p[len] != 0) len++;
            byte[] result = new byte[len];
            Marshal.Copy((IntPtr)p, result, 0, len);
            return result;

        }

        /// <summary>
        /// Handles given event
        /// </summary>
        /// <param name="e"> Event to handle </param>
        /// <returns> true if event needs to fall down (to next window in queue) </returns>
        public bool Event(Event e)
        {
            if (popup != null)
            {
                return popup.Event(e);
            }
            else
            {
                return HandleEvent(e);  
            }
        }

        public virtual bool HandleEvent(Event e)
        {
            return true;
        }

        /// <summary>
        /// Used to close this window from itself.
        /// </summary>
        public void DeleteSelf()
        {
            OnQuit?.Invoke(this);
            popup?.DeleteSelf();
            Program.windows.Remove(this);
            if (parent?.popup == this)
            {
                parent?.popup = null;
            }
            this.deleted = true;
        }
    }
}
