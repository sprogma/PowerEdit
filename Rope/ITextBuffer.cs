using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TextBuffer
{
    public interface IUndoTextBuffer : ITextBuffer
    {
        public void Undo();

        public bool Redo();

        public void SetVersion(IntPtr version);

        public IntPtr GetCurrentVersion();

        public IntPtr[] GetInitialVersions();

        public (IntPtr[] states, MarshalingLink[] links) GetVersionTree();
    }

    public interface INavigatableTextBuffer : ITextBuffer
    {
        public void SaveCursors(IntPtr state, MarshalingCursor[] cursors);
        public MarshalingCursor[] GetCursors(IntPtr state);
    }

    public interface IEditableTextBuffer : ITextBuffer
    {
        public long Insert(long index, string item);

        public long Insert(long index, byte[] item);

        public void RemoveAt(long index, long count = 1);

        public void Clear() => RemoveAt(0, Length);
    }

    public interface ITextBuffer
    {
        public char this[long index] { get; set; }

        public int Length { get; }

        public long LengthEx(IntPtr state);

        public string SubstringEx(IntPtr state, long pos, long len);

        public string SubstringEx(IntPtr state, long pos);

        public string Substring(long pos, long len);

        public string Substring(long pos);

        public long IndexOf(char item, long offset);

        public long LastIndexOf(char item, long offset);

        public long IndexOf(string item, long offset);

        public long NearestNewlineLeft(long offset);

        public long NearestNewlineRight(long offset);

        public long SetText(string text);

        public long SetBytes(byte[] text);

        public void SaveToFile(string filename);

        public (long, long) GetPositionOffsets(long position);

        public (long index, string? text, long length) GetLine(long line);
    }
}
