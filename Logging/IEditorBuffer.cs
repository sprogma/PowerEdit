using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using TextBuffer;

namespace Common
{
    public interface IEditorBuffer
    {
        public ITextBuffer Text { get; }
        public string? Filename { get; }
        public string? GivenLanguageId { get; }
        public bool TryUseLSP { get; set; }

        public bool WasChanged { get; set; }

        public string GetId();

        public void SaveCursorState();

        public void LoadCursorState();

        public void Undo();

        public void Redo();

        public long InsertString(long position, string data);

        public long InsertBytes(long position, byte[] data);

        public void DeleteString(long position, long count);
        public long SetText(string data);

        public (long offset, string? value, long length) GetLine(long line, long? maxsize);

        public (long line, long offset) GetPositionOffsets(long position);

        public long GetPosition(long line, long col);

        public (long begin, long length) GetLineOffsets(long line);

        public void Commit();

        public void Fork();

        public void SetVersion(IntPtr id);

        public string? LanguageId();

        public static string? LanguageId(string? key)
        {
            if (key == null)
            {
                return null;
            }
            var ext = Path.GetExtension(key);
            if (string.IsNullOrEmpty(ext))
            {
                return null;
            }
            return ext?[1..]?.ToLower() switch
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
