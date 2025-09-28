using EditorCore.Buffer;
using EditorCore.File;
using RegexTokenizer;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                    break;
            }
            return base.HandleEvent(e);
        }
    }
}
