using EditorFramework.ApplicationApi;
using EditorFramework.Layout;
using EditorFramework.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace EditorFramework.Widgets
{
    public class AlertWindow : BaseWindow
    {
        public string Text;
        public (string Text, Action Callback)[] Buttons;
        public int Selected = 0;

        public AlertWindow(IApplication app, ILayoutManager layout, string text, params (string text, Action callback)[] buttons) : base(app, layout)
        {
            Debug.Assert(buttons.Length > 0);
            this.Text = text;
            this.Buttons = buttons;
        }

        public override bool HandleEvent(EventBase e)
        {
            switch (e)
            {
                case QuitEvent:
                    Environment.Exit(1);
                    return false;

                case KeyChordEvent c when c.Is(KeyCode.Up):
                    Selected = (Selected + Buttons.Length - 1) % Buttons.Length;
                    return false;

                case KeyChordEvent c when c.Is(KeyCode.Down):
                    Selected = (Selected + 1) % Buttons.Length;
                    return false;

                case KeyChordEvent c when c.Is(KeyCode.Enter):
                    Buttons[Selected].Callback();
                    DeleteSelf();
                    return false;
            }
            return true;
        }
    }
}
