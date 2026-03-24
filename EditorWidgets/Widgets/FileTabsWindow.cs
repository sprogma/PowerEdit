using EditorCore.Buffer;
using EditorCore.File;
using EditorFramework.ApplicationApi;
using EditorFramework.Layout;
using Markdig.Syntax;
using Microsoft.CodeAnalysis;
using SDL_Sharp;
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

        public FileTabsWindow(IApplication App, List<FileEditorWindow> windows, Rect position) : base(App, position)
        {
            this.childs = windows;
            this.current = 0;
            Resize(position);
        }

        public override void Resize(Rect newPosition)
        {
            base.Resize(newPosition);
            newPosition.Y += tabHeight;
            newPosition.H -= tabHeight;
            foreach (var child in this.childs)
            {
                child.Resize(newPosition);
            }
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
            childs.Add(new FileEditorWindow(App, file, new(Position.X, Position.Y + tabHeight, Position.Width, Position.Height - tabHeight)));
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

        public override bool HandleEvent(Event e)
        {
            switch (e.Type)
            {
                case EventType.Quit:
                    Environment.Exit(1);
                    return false;
                case EventType.KeyDown:
                    {
                        if (Scancode.D1 <= e.Keyboard.Keysym.Scancode && e.Keyboard.Keysym.Scancode <= Scancode.D0 && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0)
                        {
                            int id = e.Keyboard.Keysym.Scancode - Scancode.D1;
                            if (id < childs.Count)
                            {
                                current = id;
                                return false;
                            }
                        }
                        else if (e.Keyboard.Keysym.Scancode == Scancode.RightBracket && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0)
                        {
                            if (childs.Count > 0)
                            {
                                current++;
                                current %= childs.Count;
                                return false;
                            }
                        }
                        else if (e.Keyboard.Keysym.Scancode == Scancode.LeftBracket && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0)
                        {
                            if (childs.Count > 0)
                            {
                                current += childs.Count - 1;
                                current %= childs.Count;
                                return false;
                            }
                        }
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
