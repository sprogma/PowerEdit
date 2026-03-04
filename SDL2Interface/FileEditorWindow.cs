using EditorCore.Buffer;
using EditorCore.File;
using EditorCore.Selection;
using RegexTokenizer;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;
using static System.Net.Mime.MediaTypeNames;

namespace SDL2Interface
{
    internal class FileEditorWindow : InputTextWindow
    {
        internal EditorFile file;

        public FileEditorWindow(EditorFile file, Rect position) : base(file.Buffer, position)
        {
            this.file = file;
        }

        public override bool HandleEvent(Event e)
        {
            switch (e.Type)
            {
                case EventType.Quit:
                    Environment.Exit(1);
                    break;
                case EventType.KeyDown:
                    if (e.Keyboard.Keysym.Scancode == Scancode.S && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        if (file.filename == null)
                        {
                            PromptTextWindow promptWindow = new(new EditorBuffer(file.Server, BaseTokenizer.CreateBaseTokenizer(), null, null, new PersistentCTextBuffer()), position);
                            promptWindow.cursor?.Buffer.Text.SetText("enter path to file to save into");
                            promptWindow.cursor?.Selections = new(promptWindow.cursor, [new EditorSelection(promptWindow.cursor, 0, promptWindow.buffer.Text.Length)]);
                            OpenPopup(promptWindow);
                            popup?.OnQuit += (x) =>
                            {
                                if (x is PromptTextWindow itw)
                                {
                                    string newFilename = itw.buffer.Text.Substring(0);
                                    if (string.IsNullOrWhiteSpace(newFilename)) {
                                        ReleasePopup();
                                        OpenPopup(new AlertWindow($"Error - Empty filename", position, ("Ok", () => { }))); 
                                        return;
                                    }
                                   
                                    try
                                    {
                                        FileInfo fi = new FileInfo(newFilename);
                                        if (Directory.Exists(newFilename))
                                        {
                                            ReleasePopup();
                                            OpenPopup(new AlertWindow($"Error - this is name of existing directory", position, ("Ok", () => { })));
                                            return;
                                        }
                                        string? directory = Path.GetDirectoryName(newFilename);
                                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                                        {
                                            ReleasePopup();
                                            OpenPopup(new AlertWindow($"Error - parent directory of file doesn't exists. Create it?", position, 
                                                    ("Yes, create", () => {
                                                        string? dirname = Path.GetDirectoryName(newFilename);
                                                        if (string.IsNullOrEmpty(dirname))
                                                        {
                                                            ReleasePopup();
                                                            OpenPopup(new AlertWindow($"Error - Empty directory name : {e}", position, ("Ok", () => { })));
                                                            return;
                                                        }
                                                        try
                                                        {
                                                            Directory.CreateDirectory(dirname);
                                                        } 
                                                        catch (Exception e)
                                                        {
                                                            ReleasePopup();
                                                            OpenPopup(new AlertWindow($"Error - failed to create directory {dirname} : {e}", position, ("Ok", () => { })));
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
                                        OpenPopup(new AlertWindow($"Error - can't save as this file: {e.Message}", position, ("Ok", ()=>{})));
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
                    }
                    else if ((e.Keyboard.Keysym.Scancode == Scancode.Q && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0))
                    {
                        if (file.WasChanged)
                        {
                            OpenPopup(new AlertWindow("Do you want to quit? all progress will be removed.", position, 
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
                                                             OpenPopup(new AlertWindow("Error: File save failed [data was't saved]", position, ("ok", () => { })));
                                                        }
                                                     }), 
                                                     ("yes, discard changes", () => { DeleteSelf(); })));
                            return false;
                        }
                        DeleteSelf();
                        return false;
                    }
                    break;
            }
            return base.HandleEvent(e);
        }
    }
}
