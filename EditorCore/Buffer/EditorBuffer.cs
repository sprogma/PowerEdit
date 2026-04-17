using Common;
using EditorCore.File;
using EditorCore.Selection;
using Lsp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using TextBuffer;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EditorCore.Buffer
{
    public delegate void EditorBufferOnUpdate(EditorBuffer buffer);
    public delegate bool EditorBufferOnTextInput(EditorBuffer buffer);
    public class EditorBuffer : IDisposable
    {
        public EditorBufferOnUpdate? ActionOnUpdate = null;
        public EditorBufferOnTextInput? ActionOnTextInput = null;

        public const long MaxHistorySize = 1024;

        public ITextBuffer Text { get; internal set; }
        public Server.EditorServer Server { get; internal set; }
        public Cursor.EditorCursor? Cursor { get; internal set; }
        public BaseTokenizer Tokenizer { get; internal set; }
        public List<Token> Tokens { get; internal set; } = [];

        public Lock ErrorMarksLock = new();
        public List<IErrorMark> ErrorMarks { get; internal set; } = [];
        public Task<LspClient>? PotentialClient { get; internal set; }
        public Task<LspClient>? Client { get; internal set; }
        public List<Func<Task>> ClientTasks = [];
        public Channel<Func<Task>> ClientPipeline = Channel.CreateUnbounded<Func<Task>>();
        public string? Filename { get; internal set; }
        public string? GivenLanguageId { get; internal set; }

        private IntPtr? last_saved_version = null;
        private bool dirty_was_changed = false;

        private Task? RunningWorker = null;

        public bool TryUseLSP { get; set; } = true;

        public bool WasChanged
        {
            get
            {
                if (Text is IUndoTextBuffer undoText)
                {
                    return last_saved_version == null || undoText.GetCurrentVersion() != undoText.ResolveVersion(last_saved_version.Value);
                }
                return dirty_was_changed;
            }
            set
            {
                dirty_was_changed = value;
                if (Text is IUndoTextBuffer undoText)
                {
                    last_saved_version = (value ? null : undoText.GetCurrentVersion());
                }
            }
        }

        private bool WasIgnored = false;

        public EditorBuffer(Server.EditorServer server, BaseTokenizer tokenizer, string? filename, string? languageId, ITextBuffer buffer)
        {
            if (filename != null) { filename = Path.GetFullPath(filename); }
            GivenLanguageId = languageId;
            Tokenizer = tokenizer;
            Text = buffer;
            Filename = filename;
            Cursor = new(this);
            Cursor.Selections.Add(new EditorSelection(Cursor, 0));
            Server = server;
            PotentialClient = Client = server.GetLspAsync(LanguageId());
            SaveCursorState();

            ActionOnUpdate += server.ActionOnBufferUpdate;
            ActionOnTextInput += server.ActionOnBufferTextInput;

            if (TryUseLSP)
            {
                RunningWorker = Task.Run(StartWorkerAsync);

                if (Client != null) { _ = Task.Run(async () => { await Client; OnUpdate(); }); }

                if (Client != null) 
                {
                    IntPtr state = Text.CurrentState;
                    if (!ClientPipeline.Writer.TryWrite(async () => await (await Client).OpenFileAsync(HandleDiagnostics, PositionCallback, Filename, GetId(), LanguageId(), Text.SubstringEx(state, 0))))
                    {
                        throw new Exception("What2?");
                    }
                }
            }

            OnUpdate();
        }

        public EditorBuffer(Server.EditorServer server, string content, BaseTokenizer tokenizer, string? filename, string? languageId, ITextBuffer buffer)
        {
            if (filename != null) { filename = Path.GetFullPath(filename); }
            GivenLanguageId = languageId;
            Tokenizer = tokenizer;
            Text = buffer;
            Filename = filename;
            Cursor = new(this);
            Cursor.Selections.Add(new EditorSelection(Cursor, 0));
            Server = server;
            PotentialClient = Client = server.GetLspAsync(LanguageId());
            SaveCursorState();

            ActionOnUpdate += server.ActionOnBufferUpdate;
            ActionOnTextInput += server.ActionOnBufferTextInput;

            if (TryUseLSP)
            {
                RunningWorker = Task.Run(StartWorkerAsync);

                if (Client != null) { _ = Task.Run(async () => { await Client; OnUpdate(); }); }

                if (Client != null) { ClientPipeline.Writer.TryWrite(async () => await (await Client).OpenFileAsync(HandleDiagnostics, PositionCallback, Filename, GetId(), LanguageId(), "")); }

                SetText(content);
            }

            OnUpdate();
        }

        private long PositionCallback(long line, long col) => GetPosition(line, col);

        private void HandleDiagnostics(string sourceId, IEnumerable<IErrorMark> values)
        {
            /* clear all previous diagnostics from this source & insert this ones */
            lock (ErrorMarksLock)
            {
                ErrorMarks = [.. ErrorMarks.Where(x => x.Source != sourceId)];
                ErrorMarks.AddRange(values);
            }
        }

        public async Task StartWorkerAsync()
        {
            await foreach (var taskFunc in ClientPipeline.Reader.ReadAllAsync())
            {
                try
                {
                    if (Client != null)
                        await taskFunc();
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, $"Error at lsp task {ex.Message}");
                }
            }
        }

        public string GetId()
        {
            return $"{RuntimeHelpers.GetHashCode(this).ToString()}.{LanguageId() ?? "unknown"}";
        }

        public void SaveCursorState()
        {
            if (Text is INavigatableTextBuffer navText && Text is IUndoTextBuffer undoText)
            {
                if (Cursor == null)
                {
                    return;
                }
                navText.SaveCursors(undoText.GetCurrentVersion(), Cursor.Selections.Select(x => new MarshalingCursor(x.Begin, x.End)).ToArray());
            }
        }

        public void LoadCursorState()
        {
            if (Text is INavigatableTextBuffer navText && Text is IUndoTextBuffer undoText)
            {
                if (Cursor == null)
                {
                    return;
                }
                var ver = undoText.GetCurrentVersion();
                var cursors = navText.GetCursors(ver);
                Cursor.Selections = new(Cursor, cursors.Select(x => new EditorSelection(Cursor, x.Begin, x.End)).ToArray());
            }
        }

        internal void OnUpdate()
        {
            if (Cursor == null)
            {
                return;
            }
            ActionOnUpdate?.Invoke(this);
            if (TryUseLSP && PotentialClient?.IsCompleted == true)
            {
                if (WasIgnored)
                {
                    /* make full syncronization */
                    if (Client != null)
                    {
                        IntPtr state = Text.CurrentState;
                        if (!ClientPipeline.Writer.TryWrite(async () => await (await Client).ChangeFileAsync(Filename, GetId(), Text.SubstringEx(state, 0))))
                        {
                            throw new InvalidOperationException("What 3?");
                        }
                    }
                    WasIgnored = false;
                }
                else
                {
                    /* make partial syncronization */
                    if (Text.Length <= PotentialClient?.Result.MaxContentSize)
                    {
                        Client = PotentialClient;
                        foreach (var x in ClientTasks)
                        {
                            if (!ClientPipeline.Writer.TryWrite(x))
                            {
                                throw new InvalidOperationException("What?");
                            }
                        }
                    }
                    else
                    {
                        Client = null;
                    }
                }
            }
            else
            {
                WasIgnored = true;
            }
            ClientTasks.Clear();
            if (Text.Length <= Tokenizer.MaxContentSize)
            {
                IntPtr state = Text.CurrentState;
                _ = Task.Run(() => Tokens = Tokenizer.ParseContent(Text.SubstringEx(state, 0)));
            }
            dirty_was_changed = true;
        }

        public void Undo()
        {
            if (Text is IUndoTextBuffer undoText)
            {
                if (Cursor == null)
                {
                    return;
                }
                SaveCursorState();
                undoText.Undo();
                LoadCursorState();

                IntPtr state = Text.CurrentState;
                if (Client != null) { ClientTasks.Add(async () => await (await Client).ChangeFileAsync(Filename, GetId(), Text.SubstringEx(state, 0))); }
                OnUpdate();
            }
        }

        public void Redo()
        {
            if (Text is IUndoTextBuffer undoText)
            {
                if (Cursor == null)
                {
                    return;
                }
                if (!undoText.Redo())
                {
                    return;
                }
                LoadCursorState();

                IntPtr state = Text.CurrentState;
                if (Client != null) { ClientTasks.Add(async () => await (await Client).ChangeFileAsync(Filename, GetId(), Text.SubstringEx(state, 0))); }
                OnUpdate();
            }
        }


        private void MoveCursorsInsert(long position, long length)
        {
            /* move all cursors */
            if (Cursor != null)
            {
                Cursor.Selections.MoveInsert(position, length);
            }
            lock (ErrorMarksLock)
            {
                Span<IErrorMark> span = CollectionsMarshal.AsSpan(ErrorMarks);
                for (int i = 0; i < span.Length; i++)
                {
                    ref var err = ref span[i];
                    if (err.Begin >= position)
                    {
                        err.Begin += length;
                    }
                    if (err.End >= position)
                    {
                        err.End += length;
                    }
                }
            }
        }

        internal long InsertString(long position, string data)
        {
            if (Text is IEditableTextBuffer editableText)
            {
                if (Client != null)
                {
                    var (line, col) = GetPositionOffsets(position);
                    ClientTasks.Add(async () => await (await Client).ChangeFileAsync(Filename, GetId(), (int)line, (int)col, data));
                }
                long length = editableText.Insert(position, data);
                MoveCursorsInsert(position, length);
                return length;
            }
            return 0;
        }

        internal long InsertBytes(long position, byte[] data)
        {
            if (Text is IEditableTextBuffer editableText)
            {
                if (Client != null)
                {
                    var (line, col) = GetPositionOffsets(position);
                    ClientTasks.Add(async () => await (await Client).ChangeFileAsync(Filename, GetId(), (int)line, (int)col, Encoding.UTF8.GetString(data)));
                }
                long length = editableText.Insert(position, data);
                MoveCursorsInsert(position, length);
                return length;
            }
            return 0;
        }

        internal void DeleteString(long position, long count)
        {
            if (Text is IEditableTextBuffer editableText)
            {
                if (Client != null) 
                { 
                    var (line, col) = GetPositionOffsets(position); 
                    var (line2, col2) = GetPositionOffsets(position + count); 
                    ClientTasks.Add(async () => await (await Client).ChangeFileAsync(Filename, GetId(), (int)line, (int)col, (int)line2, (int)col2, (int)count));
                }
                if (position + count <= 0)
                {
                    return;
                }
                if (position < 0)
                {
                    count += position;
                    position = 0;
                }
                editableText.RemoveAt(position, count);
                MoveCursorsDelete(position, count);
            }
        }

        private void MoveCursorsDelete(long position, long length)
        {

            /* move all cursors */
            if (Cursor != null)
            {
                Cursor.Selections.MoveDelete(position, length);
            }
            lock (ErrorMarksLock)
            {
                Span<IErrorMark> span = CollectionsMarshal.AsSpan(ErrorMarks);
                for (int i = 0; i < span.Length; i++)
                {
                    ref var err = ref span[i];
                    if (err.Begin >= position + length)
                    {
                        err.Begin -= length;
                    }
                    else if (err.Begin >= position)
                    {
                        err.Begin = position;
                    }
                    if (err.End >= position + length)
                    {
                        err.End -= length;
                    }
                    else if (err.End >= position)
                    {
                        err.End = position;
                    }
                }
            }
        }

        public long SetText(string data)
        {
            long res = Text.SetText(data);
            if (Client != null) { ClientTasks.Add(async () => await (await Client).ChangeFileAsync(Filename, GetId(), data)); }
            OnUpdate();
            return res;
        }

        public (long offset, string? value, long length) GetLine(long line, long? maxsize)
        {
            return Text.GetLine(line, maxsize);
        }

        public (long line, long offset) GetPositionOffsets(long position)
        {
            Debug.Assert(position >= 0);
            return Text.GetPositionOffsets(position);
        }

        public long GetPosition(long line, long col)
        {
            return Text.GetPosition(line, col);
        }

        public (long begin, long length) GetLineOffsets(long line)
        {
            return Text.GetLineOffsets(line);
        }

        public void Commit()
        {
            if (Text is IEditableTextBuffer editableText)
            {
                editableText.Commit();
            }
            SaveCursorState();
            OnUpdate();
        }

        internal void Fork()
        {
            if (Text is IEditableTextBuffer editableText)
            {
                editableText.Fork();
            }
        }

        public void SetVersion(IntPtr id)
        {
            if (Text is IUndoTextBuffer undoText)
            {
                undoText.SetVersion(id);
                LoadCursorState();
                IntPtr state = Text.CurrentState;
                if (Client != null) { ClientTasks.Add(async () => await (await Client).ChangeFileAsync(Filename, GetId(), Text.SubstringEx(state, 0))); }
                OnUpdate();
            }
        }

        internal void UpdateRename(string? newFilename)
        {
            if (newFilename != null) newFilename = Path.GetFullPath(newFilename);
            string? oldLanguage = LanguageId();
            string? newLanguage = LanguageId(newFilename);
            if (oldLanguage != newLanguage)
            {
                Tokenizer = BaseTokenizer.CreateTokenizer(newLanguage);
                if (TryUseLSP)
                {
                    if (PotentialClient != null)
                    {
                        string? oldFilename = Filename;
                        ClientPipeline.Writer.TryWrite(async () => await (await PotentialClient).ChangeFileAsync(oldFilename, GetId(), Text.Substring(0))); 
                    }
                    PotentialClient = Client = Server.GetLspAsync(newLanguage);
                    if (Client != null) 
                    { 
                        ClientPipeline.Writer.TryWrite(async () => await (await Client).OpenFileAsync(HandleDiagnostics, PositionCallback, newFilename, GetId(), newLanguage, "")); 
                    }
                    WasIgnored = true;
                }
            }
            else
            {
                if (TryUseLSP && Client != null) 
                {
                    string? oldFilename = Filename;
                    ClientTasks.Add(async () => await (await Client).RenameFileAsync(oldFilename, newFilename, GetId(), newLanguage, Text.Substring(0))); 
                }
            }
            Filename = newFilename;
        }

        bool disposed = false;

        ~EditorBuffer() 
        {
            Dispose();
        } 

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            if (disposed)
            {
                return;
            }
            disposed = true;
            if (TryUseLSP)
            {
                if (PotentialClient != null) { ClientPipeline.Writer.TryWrite(async () => await (await PotentialClient).CloseFileAsync(Filename, GetId(), LanguageId())); }
                ClientPipeline.Writer.Complete();
            }
            Text.Dispose();
        }

        public string? LanguageId()
        {
            if (GivenLanguageId != null) return GivenLanguageId;
            return LanguageId(Filename);
        }

        internal static string? LanguageId(string? key)
        {
            if (key == null)
            {
                return null;
            }
            return Path.GetExtension(key)?[1..]?.ToLower() switch
            {
                "hive" => "hive",
                "cpp" or "cxx" or "cc" or "c++" or "hpp" or "hxx" or "hh" => "cpp",
                "c" or "h" => "c",
                "d" or "di" or "dd" => "d",
                "go" => "go",
                "hs" or "lhs" => "haskell",
                "java" or "class" or "jar" => "java",
                "js" or "mjs" or "cjs" or "jsx" => "javascript",
                "lit" or "lp" => "literate",
                "lua" => "lua",
                "nim" or "nims" or "nimble" => "nim",
                "nix" => "nix",
                "m" or "mm" or "M" => "objective-c",
                "py" or "pyw" or "pyi" => "python",
                "rs" => "rust",
                "sh" or "bash" or "zsh" or "ksh" => "shellscript",
                "swift" => "swift",
                "yaml" or "yml" => "yaml",
                "cs" => "csharp",
                "html" or "htm" => "html",
                "css" or "scss" or "sass" or "less" => "css",
                "ts" or "tsx" => "typescript",
                "json" => "json",
                "sql" => "sql",
                "md" => "markdown",
                "rb" => "ruby",
                "php" => "php",
                "dockerfile" => "dockerfile",
                _ => "undefined"
            };
        }
    }
}
