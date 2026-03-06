using CommandProviderInterface;
using EditorCore.Buffer;
using EditorCore.File;
using EditorCore.Selection;
using EditorCore.Server;
using RegexTokenizer;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Text;
using TextBuffer;

namespace SDL2Interface
{
    internal class ProjectEditorWindow : BaseWindow
    {
        public List<EditorFile> Files;
        public EditorServer Server;
        protected BaseWindow Child;
        protected Action<ProjectEditorWindow, EditorFile> OpenFileCallback;
        protected Action<ProjectEditorWindow, EditorFile> RaiseFileCallback;

        public ProjectEditorWindow(Action<ProjectEditorWindow, EditorFile> openFileCallback,
                                   Action<ProjectEditorWindow, EditorFile> raiseFileCallback,
                                   BaseWindow child,
                                   ICommandProvider provider,
                                   Rect position) : base(position)
        {
            Files = [];
            Server = new(provider);
            Child = child;
            Child.Resize(position);
            OpenFileCallback = openFileCallback;
            RaiseFileCallback = raiseFileCallback;
        }

        internal EditorFile CreateFile(string? name = null, string? externsion = null)
        {
            var file = Server.CreateFile(name, externsion);
            Files.Add(file);
            OpenFileCallback(this, file);
            return file;
        }

        public EditorFile OpenFile(string filename)
        {
            var file = Files.Find(x => x.filename == filename);
            if (file != null)
            {
                RaiseFileCallback(this, file);
                return file;
            }
            file = Server.OpenFile(filename);
            Files.Add(file);
            OpenFileCallback(this, file);
            return file;
        }

        public override void DrawElements()
        {
            Child.Draw();
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
                        if (e.Keyboard.Keysym.Scancode == Scancode.T && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                        {
                            CreateFile(null, null);
                            return false;
                        }
                        if (e.Keyboard.Keysym.Scancode == Scancode.O && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                        {
                            PromptTextWindow promptWindow = new(new EditorBuffer(Server, BaseTokenizer.CreateBaseTokenizer(), null, null, new PersistentCTextBuffer()), position);
                            promptWindow.cursor?.Buffer.Text.SetText("enter path to file to open");
                            promptWindow.cursor?.Selections = new(promptWindow.cursor, [new EditorSelection(promptWindow.cursor, 0, promptWindow.buffer.Text.Length)]);
                            OpenPopup(promptWindow);
                            popup?.OnQuit += (x) =>
                            {
                                if (x is PromptTextWindow itw)
                                {
                                    string filename = itw.buffer.Text.Substring(0);
                                    if (!File.Exists(filename))
                                    {
                                        ReleasePopup();
                                        OpenPopup(new AlertWindow($"Error - File {filename} doesn't exists or is a directory", position, ("Ok", () => { })));
                                        return;
                                    }
                                    Console.WriteLine($"opening file {filename}");
                                    OpenFile(filename);
                                }
                                else
                                {
                                    throw new Exception($"Error: window isn't InputTextWindow (have {x.GetType()})");
                                }
                            };
                            return false;
                        }
                    }
                    break;
            }
            bool res = Child.Event(e);
            if (Child.deleted)
            {
                DeleteSelf();
                return false;
            }
            return res;
        }
    }
}
