using EditorCore.Buffer;
using EditorCore.File;
using EditorCore.Selection;
using RegexTokenizer;
using EditorFramework.ApplicationApi;
using EditorFramework.Layout;
using EditorFramework.Events;
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

        public FileEditorWindow(IApplication App, EditorFile file, Rect position) : base(App, file.Buffer, position)
        {
            this.file = file;
        }

        public override bool HandleEvent(BaseEvent e)
        {
            switch (e)
            {
                case EventQuit:
                    Environment.Exit(1);
                    break;
                case KeyChordEvent chord when chord.Is(KeyCode.S, KeyMode.Ctrl):
                    if (file.filename == null)
                    {
                        PromptTextWindow promptWindow = new(new EditorBuffer(file.Server, BaseTokenizer.CreateBaseTokenizer(), null, null, new PersistentCTextBuffer()), Position);
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
                                    OpenPopup(new AlertWindow(App, $"Error - Empty filename", Position, ("Ok", () => { }))); 
                                    return;
                                }
                                   
                                try
                                {
                                    FileInfo fi = new FileInfo(newFilename);
                                    if (Directory.Exists(newFilename))
                                    {
                                        ReleasePopup();
                                        OpenPopup(new AlertWindow(App, $"Error - this is name of existing directory", Position, ("Ok", () => { })));
                                        return;
                                    }
                                    string? directory = Path.GetDirectoryName(newFilename);
                                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                                    {
                                        ReleasePopup();
                                        OpenPopup(new AlertWindow(App, $"Error - parent directory of file doesn't exists. Create it?", Position, 
                                                ("Yes, create", () => {
                                                    string? dirname = Path.GetDirectoryName(newFilename);
                                                    if (string.IsNullOrEmpty(dirname))
                                                    {
                                                        ReleasePopup();
                                                        OpenPopup(new AlertWindow(App, $"Error - Empty directory name : {e}", Position, ("Ok", () => { })));
                                                        return;
                                                    }
                                                    try
                                                    {
                                                        Directory.CreateDirectory(dirname);
                                                    } 
                                                    catch (Exception e)
                                                    {
                                                        ReleasePopup();
                                                        OpenPopup(new AlertWindow(App, $"Error - failed to create directory {dirname} : {e}", Position, ("Ok", () => { })));
                                                        return;
                                                    }
                                                    Console.WriteLine($"file saved as {newFilename}");
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
                                    OpenPopup(new AlertWindow(App, $"Error - can't save as this file: {e.Message}", Position, ("Ok", ()=>{})));
                                    return;
                                }
                                    
                                Console.WriteLine($"file saved as {newFilename}");
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
                        Console.WriteLine($"file saved as {file.filename}");
                        file.Save();
                    }
                    return false;
                case KeyChordEvent chord when chord.Is(KeyCode.Q, KeyMode.Ctrl):
                    if (file.WasChanged)
                    {
                        OpenPopup(new AlertWindow(App, "Do you want to quit? all progress will be removed.", Position, 
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
                                                            OpenPopup(new AlertWindow(App, "Error: File save failed [data was't saved]", Position, ("ok", () => { })));
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
