using EditorFramework.Widgets;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorFramework.ApplicationApi
{
    public interface IApplication
    {
        public void RemoveWindow(BaseWindow window);

        public void SetClipboard(string text);

        public IEnumerable<BaseWindow> ListWindows();
    }
}
