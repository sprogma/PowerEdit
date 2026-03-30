using EditorCore.Buffer;
using EditorCore.File;
using EditorCore.Selection;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using LoggingLogLevel;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;
using static System.Net.Mime.MediaTypeNames;

namespace EditorFramework.Widgets
{
    public class FileEditorWindow : InputTextWindow
    {
        public EditorFile file;

        public FileEditorWindow(IApplication App, ILayoutManager layout, EditorFile file) : base(App, layout, file.Buffer)
        {
            this.file = file;
        }

        public override bool HandleEvent(EventBase e)
        {
            switch (e)
            {
                case QuitEvent:
                    Environment.Exit(1);
                    break;
                case KeyChordEvent chord when chord.Is(KeyCode.S, KeyMode.Ctrl):
                    if (file.filename == null)
                    {
                        PromptTextWindow promptWindow = new(App, GetLayout<PromptTextWindow>.Value, new EditorBuffer(file.Server, BaseTokenizer.CreateBaseTokenizer(), null, null, new PersistentCTextBuffer()));
                        promptWindow.cursor?.Buffer.Text.SetText("enter path to file to save into");
                        promptWindow.cursor?.Selections = new(promptWindow.cursor, [new EditorSelection(promptWindow.cursor, 0, promptWindow.buffer.Text.Length)]);
                        OpenPopup(promptWindow);
                        Popup?.OnQuit += (x) =>
                        {
                            if (x is PromptTextWindow itw)
                            {
                                string newFilename = itw.buffer.Text.Substring(0);
                                if (string.IsNullOrWhiteSpace(newFilename)) {
                                    ReleasePopup();
                                    OpenPopup(new AlertWindow(App, GetLayout<AlertWindow>.Value, $"Error - Empty filename", ("Ok", () => { }))); 
                                    return;
                                }
                                   
                                try
                                {
                                    FileInfo fi = new FileInfo(newFilename);
                                    if (Directory.Exists(newFilename))
                                    {
                                        ReleasePopup();
                                        OpenPopup(new AlertWindow(App, GetLayout<AlertWindow>.Value, $"Error - this is name of existing directory", ("Ok", () => { })));
                                        return;
                                    }
                                    string? directory = Path.GetDirectoryName(newFilename);
                                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                                    {
                                        ReleasePopup();
                                        OpenPopup(new AlertWindow(App, GetLayout<AlertWindow>.Value, $"Error - parent directory of file doesn't exists. Create it?", 
                                                ("Yes, create", () => {
                                                    string? dirname = Path.GetDirectoryName(newFilename);
                                                    if (string.IsNullOrEmpty(dirname))
                                                    {
                                                        ReleasePopup();
                                                        OpenPopup(new AlertWindow(App, GetLayout<AlertWindow>.Value, $"Error - Empty directory name : {e}", ("Ok", () => { })));
                                                        return;
                                                    }
                                                    try
                                                    {
                                                        Directory.CreateDirectory(dirname);
                                                    } 
                                                    catch (Exception e)
                                                    {
                                                        ReleasePopup();
                                                        OpenPopup(new AlertWindow(App, GetLayout<AlertWindow>.Value, $"Error - failed to create directory {dirname} : {e}", ("Ok", () => { })));
                                                        return;
                                                    }
                                                    Logger.Log($"file saved as {newFilename}");
                                                    file.Save(newFilename);
                                                    return;
                                                }),
                                                ("No, don't save file", () => { })
                                            ));
                                        return;
                                    }
                                }
                                catch (Exception e) 
                                {
                                    ReleasePopup();
                                    OpenPopup(new AlertWindow(App, GetLayout<AlertWindow>.Value, $"Error - can't save as this file: {e.Message}", ("Ok", ()=>{})));
                                    return;
                                }
                                    
                                Logger.Log($"file saved as {newFilename}");
                                file.Save(newFilename);
                            }
                            else
                            {
                                throw new Exception($"Error: window isn't InputTextWindow (have {x.GetType()})");
                            }
                        };
                        return false;
                    }
                    else
                    {
                        Logger.Log($"file saved as {file.filename}");
                        file.Save();
                    }
                    return false;
                case KeyChordEvent chord when chord.Is(KeyCode.Q, KeyMode.Ctrl):
                    if (file.WasChanged)
                    {
                        OpenPopup(new AlertWindow(App, GetLayout<AlertWindow>.Value, "Do you want to quit? all progress will be removed.",
                                                    ("no, continue edit", () => { }), 
                                                    ("save and quit", () => { 
                                                    file.Save();
                                                    if (!file.WasChanged)
                                                    {
                                                        DeleteSelf();
                                                    }
                                                    else
                                                    {
                                                            ReleasePopup();
                                                            OpenPopup(new AlertWindow(App, GetLayout<AlertWindow>.Value, "Error: File save failed [data was't saved]", ("ok", () => { })));
                                                    }
                                                    }), 
                                                    ("yes, discard changes", () => { DeleteSelf(); })));
                        return false;
                    }
                    DeleteSelf();
                    return false;
            }
            return base.HandleEvent(e);
        }
    }
}
