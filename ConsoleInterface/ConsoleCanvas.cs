using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleInterface
{
    using EditorFramework.Layout;
    using Humanizer;
    using Common;
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Text;
    using Wcwidth;
    using static System.Net.Mime.MediaTypeNames;

    public struct Color
    {
        public byte R, G, B;
        public bool IsDefault;

        public Color(byte r, byte g, byte b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.IsDefault = false;
        }

        public Color(EditorFramework.Color color)
        {
            this.R = color.r;
            this.G = color.g;
            this.B = color.b;
            this.IsDefault = false;
        }

        public static Color Default => new() { IsDefault = true };

        public readonly bool Equals(Color other) =>
            IsDefault == other.IsDefault &&
            (!IsDefault && R == other.R && G == other.G && B == other.B);

        public readonly string AsForeground
        {
            get
            {
                if (IsDefault) return "\x1b[39m";
                return $"\x1b[38;2;{R};{G};{B}m";
            }
        }

        public readonly string AsBackground
        {
            get
            {
                if (IsDefault) return "\x1b[49m";
                return $"\x1b[48;2;{R};{G};{B}m";
            }
        }
    }

    public class ConsoleCanvas : IDisposable
    {
        private struct Cell
        {
            public string Text;
            public Color Foreground;
            public Color Background;

            public readonly bool Equals(Cell other) =>
                Text == other.Text &&
                Foreground.Equals(other.Foreground) &&
                Background.Equals(other.Background);
        }

        private Cell[,] Buffer = new Cell[0, 0];
        private Cell[,] PreviousBuffer = new Cell[0, 0];
        private int LastX = -1;
        private int LastY = -1;
        private Color LastForeground = Color.Default;
        private Color LastBackground = Color.Default;
        private bool VTEnabled;

        private bool AltBufferEnabled = false;
        private Lock AltBufferLock = new();
        internal int QuitCount = 0;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public Rect ClipRect { get; set {
                if (value.Ax < 0 || value.Ay < 0 || value.Bx > Width || value.By > Height)
                {
                    throw new ArgumentException("Wrong clip rectangle");
                }
                field = value;
            } }
        public void ResetClipRect() => ClipRect = new(0, 0, Width, Height);

        public ConsoleCanvas()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            EnableVirtualTerminal();
            EnableFeatures();
            UpdateConsoleSize();
            CreateBuffers(Width, Height);
            ClipRect = new(0, 0, Width, Height);
            Console.TreatControlCAsInput = true;
            Console.CursorVisible = false;
        }

        public void EnableFeatures()
        {
            lock (AltBufferLock)
            {
                Console.Write("\x1b[?1049h");
                Console.Write("\x1b[?2004h");
                AltBufferEnabled = true;
            }
            //Console.Write("\x1b[7l");
            //Console.Write($"\x1b[1;{Console.WindowHeight - 1}r");
        }

        public void DisableFeatures()
        {
            //Console.Write("\x1b[7h");
            lock (AltBufferLock)
            {
                Console.Write("\x1b[?2004l");
                Console.Write("\x1b[?1049l");
                AltBufferEnabled = false;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            DisableFeatures();
            Console.CursorVisible = true;
        }

        public long SetCell(long x, long y, string text, Color? foreground = null, Color? background = null)
        {
            if (x < ClipRect.Ax || x >= ClipRect.Bx ||
                y < ClipRect.Ay || y >= ClipRect.By)
                return 0;

            if (string.IsNullOrEmpty(text))
                text = " ";

            long width = UnicodeCalculator.GetWidth(text);
            if (width == 2 && x + 1 >= ClipRect.Bx)
                return 0;

            Buffer[y, x].Text = text;
            if (foreground != null) Buffer[y, x].Foreground = foreground.Value;
            if (background != null) Buffer[y, x].Background = background.Value;

            if (width == 2 && x + 1 < Width)
            {
                Buffer[y, x + 1].Text = "";
                if (foreground != null) Buffer[y, x + 1].Foreground = foreground.Value;
                if (background != null) Buffer[y, x + 1].Background = background.Value;
            }

            return width;
        }

        public long AddString(long x, long y, string text, Color? foreground = null, Color? background = null)
        {
            if (y < ClipRect.Ay || y >= ClipRect.By)
                return 0;

            long currentX = x;
            long totalWidth = 0;

            var enumerator = StringInfo.GetTextElementEnumerator(text);
            while (enumerator.MoveNext())
            {
                string grapheme = enumerator.GetTextElement();
                long charWidth = UnicodeCalculator.GetWidth(grapheme);

                if (currentX + charWidth > ClipRect.Bx)
                    break;

                SetCell(currentX, y, grapheme, foreground, background);
                totalWidth += charWidth;
                currentX += charWidth;
            }

            return totalWidth;
        }

        public void ApplyStyle(Rect rect, Color? foreground = null, Color? background = null)
        {
            long intersectLeft = Math.Max(rect.Ax, ClipRect.Ax);
            long intersectTop = Math.Max(rect.Ay, ClipRect.Ay);
            long intersectRight = Math.Min(rect.Bx - 1, ClipRect.Bx - 1);
            long intersectBottom = Math.Min(rect.By - 1, ClipRect.By - 1);

            if (intersectLeft > intersectRight || intersectTop > intersectBottom)
                return;

            for (long row = intersectTop; row <= intersectBottom; row++)
            {
                for (long col = intersectLeft; col <= intersectRight; col++)
                {
                    if (Buffer[row, col].Text == "")
                        continue;

                    if (foreground.HasValue)
                        Buffer[row, col].Foreground = foreground.Value;
                    if (background.HasValue)
                        Buffer[row, col].Background = background.Value;

                    int charWidth = UnicodeCalculator.GetWidth(Buffer[row, col].Text);
                    if (charWidth == 2 && col + 1 < Width)
                    {
                        if (foreground.HasValue)
                            Buffer[row, col + 1].Foreground = foreground.Value;
                        if (background.HasValue)
                            Buffer[row, col + 1].Background = background.Value;
                    }
                }
            }
        }

        public void FillRect(Rect rect, string value, Color? foreground, Color? background)
        {
            long intersectLeft = Math.Max(rect.Ax, ClipRect.Ax);
            long intersectTop = Math.Max(rect.Ay, ClipRect.Ay);
            long intersectRight = Math.Min(rect.Bx - 1, ClipRect.Bx - 1);
            long intersectBottom = Math.Min(rect.By - 1, ClipRect.By - 1);

            if (intersectLeft > intersectRight || intersectTop > intersectBottom)
                return;

            int charWidth = UnicodeCalculator.GetWidth(value);
            for (long row = intersectTop; row <= intersectBottom; row++)
            {
                for (long col = intersectLeft; col + charWidth <= intersectRight; col++)
                {
                    col += SetCell((int)col, (int)row, value, foreground, background);
                }
            }
        }


        public void Clear()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Buffer[y, x].Text = " ";
                    Buffer[y, x].Foreground = Color.Default;
                    Buffer[y, x].Background = Color.Default;
                }
            }
        }

        public void Flush()
        {
            if (CheckConsoleResize())
                return;

            LastX = -1;
            LastY = -1;
            LastForeground = Color.Default;
            LastBackground = Color.Default;

            StringBuilder buffer = new();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (Buffer[y, x].Text == "")
                        continue;

                    Cell current = Buffer[y, x];
                    Cell previous = PreviousBuffer[y, x];

                    if (current.Equals(previous))
                        continue;

                    if (LastX != x || LastY != y)
                    {
                        buffer.Append($"\x1b[{y + 1};{x + 1}H");
                        LastX = x;
                        LastY = y;
                    }

                    if (!LastForeground.Equals(current.Foreground))
                    {
                        buffer.Append(current.Foreground.AsForeground);
                        LastForeground = current.Foreground;
                    }

                    if (!LastBackground.Equals(current.Background))
                    {
                        buffer.Append(current.Background.AsBackground);
                        LastBackground = current.Background;
                    }

                    if (current.Text != "\r\n" && (current.Text.Length != 1 || !char.IsControl(current.Text[0])))
                    {
                        buffer.Append(current.Text);
                        LastX += UnicodeCalculator.GetWidth(current.Text);
                    }
                    else
                    {
                        buffer.Append('�');
                        LastX += 1;
                    }

                    PreviousBuffer[y, x] = current;
                }
            }

            if (!LastForeground.IsDefault || !LastBackground.IsDefault)
            {
                buffer.Append("\x1b[0m");
            }

            Console.Write(buffer);
        }

        private const uint INPUT_ENABLE_CONSOLE_INPUT = 0x0004u;
        private const uint INPUT_ENABLE_LINE_INPUT = 0x0002u;
        private const uint INPUT_ENABLE_MOUSE_INPUT = 0x0010u;
        private const uint INPUT_ENABLE_EXTENDED_FLAGS = 0x0080u;
        private const uint INPUT_ENABLE_WINDOW_INPUT = 0x0008u;

        private void EnableVirtualTerminal()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IntPtr hOut = GetStdHandle(STD_OUTPUT_HANDLE);
                if (GetConsoleMode(hOut, out uint mode))
                {
                    mode |= ENABLE_VIRTUAL_TERMINAL_OUTPUT;
                    SetConsoleMode(hOut, mode);
                    VTEnabled = true;
                }
                IntPtr hIn = GetStdHandle(STD_INPUT_HANDLE);
                if (GetConsoleMode(hIn, out uint modeIn))
                {
                    // i don't know why, but this enable capturing of Ctrl+S event:
                    // https://stackoverflow.com/questions/39695431/ctrl-s-input-event-in-windows-console-with-readconsoleinputw
                    modeIn |= INPUT_ENABLE_WINDOW_INPUT | INPUT_ENABLE_MOUSE_INPUT | INPUT_ENABLE_EXTENDED_FLAGS;
                    modeIn &= ~(INPUT_ENABLE_LINE_INPUT | INPUT_ENABLE_CONSOLE_INPUT);
                    SetConsoleMode(hIn, modeIn);
                }
            }
            else
            {
                VTEnabled = true;
            }
        }

        private void UpdateConsoleSize()
        {
            Width = Console.WindowWidth;
            Height = Console.WindowHeight;
        }

        private bool CheckConsoleResize()
        {
            int newWidth = Console.WindowWidth;
            int newHeight = Console.WindowHeight;

            if (newWidth != Width || newHeight != Height)
            {
                Resize(newWidth, newHeight);
                return true;
            }
            return false;
        }

        private void Resize(int newWidth, int newHeight)
        {
            Width = newWidth;
            Height = newHeight;
            CreateBuffers(Width, Height);
            ResetClipRect();
        }

        private void CreateBuffers(int width, int height)
        {
            var newBuffer = new Cell[height, width];
            var newPrevBuffer = new Cell[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    newBuffer[y, x].Text = " ";
                    newBuffer[y, x].Foreground = Color.Default;
                    newBuffer[y, x].Background = Color.Default;
                    newPrevBuffer[y, x] = newBuffer[y, x];
                }
            }

            Buffer = newBuffer;
            PreviousBuffer = newPrevBuffer;
        }

        internal static void SetClipboard(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            string base64 = Convert.ToBase64String(bytes);
            string osc52 = $"\x1b]52;c;{base64}\x07";
            Console.Write(osc52);
        }

        private const int STD_OUTPUT_HANDLE = -11;
        private const int STD_INPUT_HANDLE = -10;
        private const uint ENABLE_VIRTUAL_TERMINAL_OUTPUT = 0x0004;
        private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    }
}
