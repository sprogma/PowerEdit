using EditorFramework.ApplicationApi;
using EditorFramework.Layout;
using EditorFramework.Events;
using SDL2Interface;
using System;
using System.Collections.Generic;
using System.Drawing.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace EditorFramework.Widgets
{
    public delegate void OnQuitAction(BaseWindow window);

    public abstract class BaseWindow
    {
        public bool IsDeleted = false;
        public IApplication App;
        public ILayoutManager Layout;
        public BaseWindow? Popup = null;
        public BaseWindow? Parent = null;
        public OnQuitAction? OnQuit = null;

        public BaseWindow(IApplication app, ILayoutManager layout)
        {
            this.App = app;
            this.Layout = layout;
        }

        public void ReleasePopup()
        {
            Popup?.Parent = null;
            Popup = null;
        }

        public void OpenPopup(BaseWindow window)
        {
            if (Popup != null)
            {
                throw new Exception("Open popup while having one");
            }
            Popup = window;
            window.Parent = this;
        }

        public virtual void PreDraw()
        {
        }

        public virtual void AfterDraw()
        {
        }

        /// <summary>
        /// Handles given event
        /// </summary>
        /// <param name="e"> Event to handle </param>
        /// <returns> true if event needs to fall down (to next window in queue) </returns>
        public bool Event(BaseEvent e)
        {
            if (Popup != null)
            {
                return Popup.Event(e);
            }
            else
            {
                return HandleEvent(e);  
            }
        }

        public virtual bool HandleEvent(BaseEvent e)
        {
            return true;
        }

        /// <summary>
        /// Used to close this window from itself.
        /// </summary>
        public void DeleteSelf()
        {
            OnQuit?.Invoke(this);
            Popup?.DeleteSelf();
            App.RemoveWindow(this);
            if (Parent?.Popup == this)
            {
                Parent?.Popup = null;
            }
            this.IsDeleted = true;
        }
    }
}
