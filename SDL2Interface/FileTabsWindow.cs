using EditorCore.Buffer;
using EditorCore.File;
using Markdig.Syntax;
using Microsoft.CodeAnalysis;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Text;
using TextBuffer;

namespace SDL2Interface
{
    internal class FileTabsWindow : BaseWindow
    {
        public List<FileEditorWindow> childs;
        public int current;
        public int tabWidth = 80;
        public int tabHeight = 25;

        internal FileEditorWindow? Child => current < childs.Count ? childs[current] : null;

        public FileTabsWindow(List<FileEditorWindow> windows, Rect position) : base(position)
        {
            this.childs = windows;
            this.current = 0;
            Resize(position);
        }

        public override void Resize(Rect newPosition)
        {
            base.Resize(newPosition);
            newPosition.Y += tabHeight;
            newPosition.Height -= tabHeight;
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
            childs.Add(new FileEditorWindow(file, new(position.X, position.Y + tabHeight, position.Width, position.Height - tabHeight)));
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

        public override void DrawElements()
        {
            /* draw tabs */
            {
                Rect header = new(position.X, position.Y, position.Width, tabHeight);
                SDL.SetRenderDrawColor(renderer, 0, 25, 40, 255);
                SDL.RenderFillRect(renderer, ref header);
                Rect tab = new(position.X + 2, position.Y + 2, tabWidth - 4, tabHeight - 4);
                SDL.RenderGetClipRect(renderer, out Rect clip);
                foreach (var (id, child) in childs.Index())
                {
                    SDL.RenderSetClipRect(renderer, ref tab);
                    if (current == id)
                    {
                        SDL.SetRenderDrawColor(renderer, 0, 50, 80, 255);
                        SDL.RenderFillRect(renderer, ref tab);
                    }
                    long dummyValue = 0;
                    textRenderer.DrawTextLine(tab.X, tab.Y, child.file.filename ?? "<Unnamed>", 0, [], ref dummyValue);
                    tab.X += tab.Width + 4;
                }
                SDL.RenderSetClipRect(renderer, ref clip);
            }
            if (current < childs.Count)
            {
                childs[current].Draw();
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
            if (Child?.deleted == true)
            {
                childs.Remove(Child);
                current = Math.Min(current, childs.Count);
            }
            return res ?? true;
        }
    }
}
