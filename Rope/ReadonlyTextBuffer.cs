using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextBuffer
{
    public class ReadonlyTextBuffer : ITextBuffer
    {
        private string content = string.Empty;
        private List<long> lineOffsets = [0];

        public ReadonlyTextBuffer() {}

        public ReadonlyTextBuffer(string content)
        {
            SetText(content);
        }

        public char this[long index]
        {
            get => content[(int)index];
            set => throw new NotSupportedException();
        }

        public int Length => content.Length;

        public long SetText(string text)
        {
            content = text ?? string.Empty;
            CalculateOffsets();
            return content.Length;
        }

        public long SetBytes(byte[] bytes)
        {
            content = Encoding.UTF8.GetString(bytes);
            CalculateOffsets();
            return content.Length;
        }

        private void CalculateOffsets()
        {
            lineOffsets.Clear();
            lineOffsets.Add(0);
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] == '\n')
                {
                    lineOffsets.Add(i + 1);
                }
            }
        }

        public (long index, string? text, long length) GetLine(long line)
        {
            if (line < 0 || line >= lineOffsets.Count) return (0, null, 0);
            long start = lineOffsets[(int)line];
            long end = (line + 1 < lineOffsets.Count) ? lineOffsets[(int)line + 1] : content.Length;
            string text = content.Substring((int)start, (int)(end - start));
            return (start, text, text.Length);
        }

        public long GetPosition(long line, long col)
        {
            if (line < 0 || line >= lineOffsets.Count) return 0;
            return lineOffsets[(int)line] + col;
        }

        public long NearestNewlineLeft(long offset)
        {
            if (offset <= 0) return 0;
            if (offset >= content.Length) offset = content.Length;

            int lineIndex = lineOffsets.BinarySearch(offset);
            if (lineIndex >= 0) return lineOffsets[lineIndex];
            int prevLine = (~lineIndex) - 1;
            return lineOffsets[prevLine];
        }

        public long NearestNewlineRight(long offset)
        {
            if (offset < 0) offset = 0;
            if (offset >= content.Length) return content.Length;

            int lineIndex = lineOffsets.BinarySearch(offset);
            if (lineIndex >= 0)
            {
                return (lineIndex + 1 < lineOffsets.Count) ? lineOffsets[lineIndex + 1] - 1 : content.Length;
            }

            int nextLine = ~lineIndex;
            return (nextLine < lineOffsets.Count) ? lineOffsets[nextLine] - 1 : content.Length;
        }

        public (long, long) GetPositionOffsets(long position)
        {
            long lineStart = NearestNewlineLeft(position);
            return (lineStart, position - lineStart);
        }

        public long IndexOf(char item, long offset) => content.IndexOf(item, (int)offset);
        public long IndexOf(string item, long offset) => content.IndexOf(item, (int)offset);
        public long LastIndexOf(char item, long offset) => content.LastIndexOf(item, (int)offset);

        public string Substring(long pos, long len) => content.Substring((int)pos, (int)len);
        public string Substring(long pos) => content.Substring((int)pos);
        public string SubstringEx(IntPtr state, long pos, long len) => Substring(pos, len);
        public string SubstringEx(IntPtr state, long pos) => Substring(pos);
        public long LengthEx(IntPtr state) => Length;
        public void SaveToFile(string filename) => File.WriteAllText(filename, content);
    }

}
