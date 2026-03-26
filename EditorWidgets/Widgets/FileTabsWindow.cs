using EditorCore.Buffer;
using EditorCore.File;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using Markdig.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using TextBuffer;

namespace EditorFramework.Widgets
{
    public class FileTabsWindow : BaseWindow
    {
        public List<FileEditorWindow> childs;
        public int current;

        public FileEditorWindow? Child => current < childs.Count ? childs[current] : null;

        public FileTabsWindow(IApplication App, ILayoutManager layout, List<FileEditorWindow> windows) : base(App, layout)
        {
            this.childs = windows;
            this.current = 0;
        }

        public void OpenFile(ProjectEditorWindow Project, EditorFile file)
        {
            for (int i = 0; i < childs.Count; i++)
            {
                if (childs[i].file == file)
                {
                    current = i;
                    return;
                }
            }
            childs.Add(new FileEditorWindow(App, GetLayout<FileEditorWindow>.Value, file));
            current = childs.Count - 1;
        }

        public void RaiseFile(ProjectEditorWindow Project, EditorFile file)
        {
            for (int i = 0; i < childs.Count; i++)
            {
                if (childs[i].file == file)
                {
                    current = i;
                    return;
                }
            }
        }

        public override bool HandleEvent(EventBase e)
        {
            switch (e)
            {
                case QuitEvent:
                    Environment.Exit(1);
                    return false;
                case KeyChordEvent k when KeyCode.D0 <= k.LastKey.Key && k.LastKey.Key <= KeyCode.D9 && k.LastKey.Mode.HasFlag(KeyMode.Alt):
                    int id = (int)k.LastKey.Key & 0xF;
                    if (id < childs.Count)
                    {
                        current = id;
                        return false;
                    }
                    break;
                case KeyChordEvent k when k.Is(KeyCode.OpenBrackets, KeyMode.Alt):
                    if (childs.Count > 0)
                    {
                        current++;
                        current %= childs.Count;
                        return false;
                    }
                    break;
                case KeyChordEvent k when k.Is(KeyCode.CloseBrackets, KeyMode.Alt):
                    if (childs.Count > 0)
                    {
                        current += childs.Count - 1;
                        current %= childs.Count;
                        return false;
                    }
                    break;
            }

            bool? res = Child?.Event(e);
            if (Child?.IsDeleted == true)
            {
                childs.Remove(Child);
                current = Math.Max(current - 1, 0);
            }
            return res ?? true;
        }
    }
}
