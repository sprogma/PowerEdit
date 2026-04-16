using Common;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TextBuffer
{
    public class PersistentCTextBuffer : IUndoTextBuffer, INavigatableTextBuffer, IEditableTextBuffer
    {
        IntPtr project;
        IntPtr curr_state;
        Stack<IntPtr> undos;
        List<IntPtr> InitialVersions;

        public PersistentCTextBuffer()
        {
            if (!CLibrary.WasInitializated) CLibrary.Init();

            project = CLibrary.project_create();
            curr_state = CLibrary.project_new_state(project);
            CLibrary.state_commit(project, curr_state);
            undos = [];
            InitialVersions = [curr_state];
        }

        public PersistentCTextBuffer(string filename)
        {
            if (!CLibrary.WasInitializated) CLibrary.Init();

            project = CLibrary.project_create();
            curr_state = CLibrary.project_open_file(project, filename);
            if (curr_state == 0)
            {
                curr_state = CLibrary.project_new_state(project);
            }
            CLibrary.state_commit(project, curr_state);
            undos = [];
            InitialVersions = [curr_state];
        }

        public char this[long index] { 
            get
            {
                IntPtr destPtr = Marshal.AllocHGlobal(1);
                CLibrary.state_read(curr_state, index, 1, destPtr);
                string res = Marshal.PtrToStringAnsi(destPtr, 1);
                Marshal.FreeHGlobal(destPtr);
                return res[0];
            }
        }

        public IntPtr CurrentState => curr_state;

        public long Length => CLibrary.state_get_size(curr_state);
        public long LengthEx(IntPtr state) => CLibrary.state_get_size(state);

        public byte[] SubBytes(long pos, long len)
        {
            byte[] data = new byte[len];
            CLibrary.state_read(curr_state, pos, len, data);
            return data;
        }

        public string SubstringEx(IntPtr state, long pos, long len)
        {
            IntPtr destPtr = Marshal.AllocHGlobal((int)(len + 10));
            CLibrary.state_read(state, pos, len, destPtr);
            string res = Marshal.PtrToStringAnsi(destPtr, (int)len);
            Marshal.FreeHGlobal(destPtr);
            return res;
        }
        
        public string SubstringEx(IntPtr state, long pos) => SubstringEx(state, pos, LengthEx(state) - pos);

        public string Substring(long pos, long len)
        {
            // Logger.Log($"Req SUBSTR of len {len}");
            IntPtr destPtr = Marshal.AllocHGlobal((int)(len + 10));

            CLibrary.state_read(curr_state, pos, len, destPtr);
            string res = Marshal.PtrToStringUTF8(destPtr, (int)len);
            Marshal.FreeHGlobal(destPtr);
            return res;
        }

        public string Substring(long pos) => Substring(pos, Length - pos);

        public long IndexOf(char item, long offset)
        {
            for (long i = offset; i < Length; ++i)
            {
                if (this[i] == item)
                {
                    return i;
                }
            }
            return -1;
        }

        public long LastIndexOf(char item, long offset)
        {
            for (long i = offset; i >= 0; --i)
            {
                if (this[i] == item)
                {
                    return i;
                }
            }
            return -1;
        }

        public long IndexOf(string item, long offset)
        {
            long l = Length;
            for (long i = Math.Max(offset, 0); i + item.Length <= l; ++i)
            {
                if (Substring(i, item.Length) == item)
                {
                    return i;
                }
            }
            return -1;
        }

        public long NearestNewlineLeft(long offset)
        {
            return CLibrary.state_nearest_left(curr_state, offset);
        }

        public long NearestNewlineRight(long offset)
        {
            return CLibrary.state_nearest_right(curr_state, offset);
        }

        public void Undo()
        {
            undos.Push(curr_state);
            curr_state = CLibrary.state_version_before(curr_state, 1);
        }

        public bool Redo()
        {
            if (undos.TryPop(out IntPtr version))
            {
                curr_state = version;
                return true;
            }
            return false;
        }

        public long Insert(long index, string item)
        {
            undos.Clear();
            byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(item);
            CLibrary.state_moditify(project, curr_state, index, Modification.Insert, utf8Bytes.Length, utf8Bytes);
            return utf8Bytes.Length;
        }

        public long Insert(long index, byte[] item)
        {
            undos.Clear();
            CLibrary.state_moditify(project, curr_state, index, Modification.Insert, item.Length, item);
            return item.Length;
        }

        public void RemoveAt(long index, long count = 1)
        {
            if (count == 0) return;
            if (index + count > Length) return;
            undos.Clear();
            CLibrary.state_moditify(project, curr_state, index, Modification.Delete, count, null);
        }

        public void Clear() => RemoveAt(0, Length);

        public void PushHistory()
        {
            // TODO: this
        }

        public void SetVersion(IntPtr version)
        {
            curr_state = CLibrary.state_resolve(version);
        }

        public IntPtr[] GetInitialVersions()
        {
            return InitialVersions.ToArray();
        }

        public IntPtr GetCurrentVersion()
        {
            return CLibrary.state_resolve(curr_state);
        }

        public (IntPtr[] states, MarshalingLink[] links) GetVersionTree()
        {
            curr_state = CLibrary.state_resolve(curr_state);
            CLibrary.project_get_states_len(project, out long versions_count, out long links_count);
            IntPtr[] states = new IntPtr[versions_count];
            MarshalingLink[] links = new MarshalingLink[links_count];
            CLibrary.project_get_states(project, versions_count, states, links_count, links);
            return (states, links);
        }


        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ReplaceFile(
                string lpReplacedFileName,
                string lpReplacementFileName,
                string lpBackupFileName,
                uint dwReplaceFlags,
                IntPtr lpExclude,
                IntPtr lpReserved);
        const int ERROR_FILE_NOT_FOUND = 2;

        public void SaveToFile(string targetFile)
        {
            string sessionGuid = Guid.NewGuid().ToString("N");
            string tempFile = targetFile + "." + sessionGuid + ".tmp";
            string backupFile = targetFile + "." + sessionGuid + ".bak";

            try
            {
                if (CLibrary.project_save_file(project, curr_state, tempFile) != 0)
                {
                    throw new Exception($"Error while saving file to <{tempFile}>");
                }
                try
                {
                    File.Move(targetFile, backupFile, true);
                }
                catch (IOException)
                {
                    Logger.Log(LogLevel.Warning, "Can't backup file, may be this is first save.");
                }
                File.Move(tempFile, targetFile, false);
                //if (!ReplaceFile(targetFile, tempFile, backupFile, 0, 0, 0))
                //{
                //    int error = Marshal.GetLastWin32Error();
                //    if (error != ERROR_FILE_NOT_FOUND)
                //    {
                //        throw new IOException($"Atomic replace failed. Win32 Error: {error}");
                //    }
                //    /* this may be first save - store using move */
                //    File.Move(tempFile, targetFile, false);
                //}
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, $"Failed to save file: {ex.Message}");
                try { File.Delete(tempFile); } catch (IOException) { Logger.Log(LogLevel.Warning, "temporary file deletion after failed saving failed"); }
            }
            /* mark file as deleted */
            try { File.Delete(backupFile); } catch (IOException) { Logger.Log(LogLevel.Warning, "backup file deletion failed"); }
        }

        public void SaveCursors(IntPtr state, MarshalingCursor[] cursors)
        {
            CLibrary.state_set_cursors(state, cursors.LongLength, cursors);
        }

        public MarshalingCursor[] GetCursors(IntPtr state)
        {
            long cursors_count = CLibrary.state_get_cursors_count(state);
            MarshalingCursor[] cursors = new MarshalingCursor[cursors_count];
            CLibrary.state_get_cursors(state, cursors_count, cursors);
            return cursors;
        }

        public (long, long) GetPositionOffsets(long position)
        {
            CLibrary.state_get_offsets(curr_state, position, out long line, out long column);
            return (line, column);
        }

        public long GetPosition(long line, long col)
        {
            if (line < 0) return 0;

            long startPos = 0;
            if (line > 0)
            {
                long prevNewline = CLibrary.state_nth_newline(curr_state, line - 1);
                if (prevNewline == -1) return 0;
                startPos = prevNewline + 1;
            }

            if (startPos >= Length)
            {
                return 0;
            }

            return startPos + col;
        }

        public (long index, long length) GetLineOffsets(long line)
        {
            if (line < 0) return (0, 0);

            long startPos = 0;
            if (line > 0)
            {
                long prevNewline = CLibrary.state_nth_newline(curr_state, line - 1);
                if (prevNewline == -1) return (0, 0);
                startPos = prevNewline + 1;
            }
            if (startPos >= Length)
            {
                return (0, 0);
            }
            long nextNewline = CLibrary.state_nth_newline(curr_state, line), len;
            if (nextNewline == -1)
            {
                len = Length - startPos;
            }
            else
            {
                len = (nextNewline - startPos) + 1;
            }
            return (startPos, len);
        }

        public (long index, string? text, long length) GetLine(long line)
        {
            (long index, long length) = GetLineOffsets(line);
            if (length == 0) return (0, null, 0);
            return (index, Substring(index, length), length);
        }

        public long SetText(string text)
        {
            Fork();
            Clear();
            long res = Insert(0, text);
            Commit();
            return res;
        }

        public long SetBytes(byte[] text)
        {
            Fork();
            Clear();
            long res = Insert(0, text);
            Commit();
            return res;
        }

        public void Fork()
        {
            curr_state = CLibrary.state_create_dup(project, curr_state);
        }

        public void Commit()
        {
            CLibrary.state_commit(project, curr_state);
        }

        public IntPtr ResolveVersion(IntPtr version)
        {
            return CLibrary.state_resolve(version);
        }
        
        ~PersistentCTextBuffer()
        {
            Dispose();
        }

        private bool IsDisposed = false;

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            GC.SuppressFinalize(this);
            //Logger.Log(LogLevel.Warning, $"FREE PROJECT AT {project} FROM BUFFER {RuntimeHelpers.GetHashCode(this)}");
            CLibrary.project_destroy(project);
        }
    }
}
