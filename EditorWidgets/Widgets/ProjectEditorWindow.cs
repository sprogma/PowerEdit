using CommandProviderInterface;
using EditorCore.Buffer;
using EditorCore.File;
using EditorCore.Selection;
using EditorCore.Server;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using Common;
using RegexTokenizer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TextBuffer;

namespace EditorFramework.Widgets
{
    public class ProjectEditorWindow : BaseWindow
    {
        // todo: make when will be async
        // public readonly ConcurrentDictionary<string, Task<EditorFile>> OpeningFiles = new();
        public EditorServer Server;
        public BaseWindow Child;
        protected Action<ProjectEditorWindow, EditorFile> OpenFileCallback;
        protected Action<ProjectEditorWindow, EditorFile> RaiseFileCallback;

        public ProjectEditorWindow(IApplication app, ILayoutManager layout,
                                   EditorServer server,
                                   Action<ProjectEditorWindow, EditorFile> openFileCallback,
                                   Action<ProjectEditorWindow, EditorFile> raiseFileCallback,
                                   BaseWindow child) : base(app, layout)
        {
            Server = server;
            Child = child;
            OpenFileCallback = openFileCallback;
            RaiseFileCallback = raiseFileCallback;
        }

        public EditorFile CreateFile(string? name = null, string? languageId = null)
        {
            var file = Server.CreateFile(name, languageId);
            OpenFileCallback(this, file);
            return file;
        }

        public EditorFile OpenFile(string filename)
        {
            filename = Path.GetFullPath(filename);
            EditorFile? file;
            using (Server.FilesLock.EnterScope())
            {
                file = Server.Files.Find(x => x.filename == filename);
                if (file != null)
                {
                    RaiseFileCallback(this, file);
                    return file;
                }
                Server.OpeningFiles++;
                file = Server.OpenFile(filename);
            }
            OpenFileCallback(this, file);
            return file;
        }

        public override bool HandleEvent(EventBase e)
        {
            switch (e)
            {
                case QuitEvent:
                    Environment.Exit(1);
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.T, KeyMode.Ctrl):
                    CreateFile(null, null);
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.O, KeyMode.Ctrl):
                    PromptTextWindow promptWindow = new(App, GetLayout<PromptTextWindow>.Value, new EditorBuffer(Server, BaseTokenizer.CreateBaseTokenizer(), null, null, null, new PersistentCTextBuffer()));
                    promptWindow.cursor?.Buffer.Text.SetText("enter path to file to open");
                    promptWindow.cursor?.Selections = new(promptWindow.cursor, [new EditorSelection(promptWindow.cursor, 0, promptWindow.buffer.Text.Length)]);
                    OpenPopup(promptWindow);
                    Popup?.OnQuit += (x) =>
                    {
                        if (x is PromptTextWindow itw)
                        {
                            string filename = itw.buffer.Text.Substring(0);
                            if (!File.Exists(filename))
                            {
                                ReleasePopup();
                                OpenPopup(new AlertWindow(App, GetLayout<AlertWindow>.Value, $"Error - File {filename} doesn't exists or is a directory", ("Ok", () => { })));
                                return;
                            }
                            Logger.Log($"opening file {filename}");
                            OpenFile(filename);
                        }
                        else
                        {
                            throw new Exception($"Error: window isn't InputTextWindow (have {x.GetType()})");
                        }
                    };
                    return false;
            }
            bool res = Child.Event(e);
            if (Child.IsDeleted)
            {
                DeleteSelf();
                return false;
            }
            if (Server.Files.Count == 0 && Server.OpeningFiles <= 0)
            {
                DeleteSelf();
                return false;
            }
            return res;
        }
    }
}
