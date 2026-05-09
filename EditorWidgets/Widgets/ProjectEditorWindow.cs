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
        protected Action<ProjectEditorWindow, EditorFile, bool> OpenFileCallback;
        protected Action<ProjectEditorWindow, EditorFile> RaiseFileCallback;

        private readonly Thread OpenFileManager;

        public const int MaxParallelOpeningFiles = 128;
        private readonly ConcurrentQueue<(string Filename, bool RaiseFocus)> FilesToOpen = new();
        private readonly Semaphore MaxThreads = new(MaxParallelOpeningFiles, MaxParallelOpeningFiles);
        private readonly AutoResetEvent WorkSignal = new(false);


        public ProjectEditorWindow(IApplication app, ILayoutManager layout,
                                   EditorServer server,
                                   Action<ProjectEditorWindow, EditorFile, bool> openFileCallback,
                                   Action<ProjectEditorWindow, EditorFile> raiseFileCallback,
                                   BaseWindow child) : base(app, layout)
        {
            Server = server;
            Child = child;
            OpenFileCallback = openFileCallback;
            RaiseFileCallback = raiseFileCallback;

            OpenFileManager = new Thread(ManagerLoop) { IsBackground = true };
            OpenFileManager.Start();

        }

        public EditorFile CreateFile(string? name = null, string? languageId = null, bool raiseFocus = true)
        {
            var file = Server.CreateFile(name, languageId);
            OpenFileCallback(this, file, raiseFocus);
            return file;
        }

        
        public void OpenFile(string filename, bool raiseFocus = true)
        {
            string? tmpname = Path.TryGetFullPath(filename);
            if (tmpname == null) return;
            filename = tmpname;
            EditorFile? file;
            using (Server.FilesLock.EnterScope())
            {
                file = Server.Files.Find(x => x.filename == filename);
                if (file != null && raiseFocus)
                {
                    RaiseFileCallback(this, file);
                }
                Interlocked.Increment(ref Server.OpeningFiles);
            }

            FilesToOpen.Enqueue((filename, raiseFocus));
            WorkSignal.Set();
        }

        private void ManagerLoop()
        {
            while (true)
            {
                if (FilesToOpen.IsEmpty) WorkSignal.WaitOne();

                if (FilesToOpen.TryDequeue(out var tuple))
                {
                    MaxThreads.WaitOne();

                    var worker = new Thread(() =>
                    {
                        try
                        {
                            var file = Server.OpenFile(tuple.Filename);
                            OpenFileCallback(this, file, tuple.RaiseFocus);
                        }
                        finally
                        {
                            MaxThreads.Release();
                        }
                    });
                    worker.IsBackground = true;
                    worker.Start();
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
                case KeyChordEvent key when key.Is(KeyCode.T, KeyMode.Ctrl):
                    CreateFile(null, null);
                    return false;
                case KeyChordEvent key when key.Is([new KeyBindingItem(KeyCode.G, KeyMode.Alt),
                                                    new KeyBindingItem(KeyCode.A, KeyMode.Alt),
                                                    new KeyBindingItem(KeyCode.M, KeyMode.Alt),
                                                    new KeyBindingItem(KeyCode.E, KeyMode.Alt)]):
                    OpenPopup(new SimpleGameWindow(App, GetLayout<SimpleGameWindow>.Value));
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.O, KeyMode.Ctrl):
                    PromptTextWindow promptWindow = new(App, GetLayout<PromptTextWindow>.Value, new EditorBuffer(Server, BaseTokenizer.CreateBaseTokenizer(), null, null, new PersistentCTextBuffer()));
                    promptWindow.cursor?.Buffer.Text.SetText("enter path to file to open");
                    promptWindow.cursor?.Selections = new(promptWindow.cursor, [new EditorSelection(promptWindow.cursor, 0, promptWindow.buffer.Text.Length)]);
                    OpenPopup(promptWindow);
                    Popup?.OnQuit += (x) =>
                    {
                        if (x is PromptTextWindow itw)
                        {
                            string? filename = itw.buffer.Text.Substring(0);
                            filename = Path.TryGetFullPath(filename);
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
