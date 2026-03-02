using EditorCore.Buffer;
using EditorCore.File;
using RegexTokenizer;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                        file.Save();
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
