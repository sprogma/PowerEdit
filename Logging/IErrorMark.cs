using System;
using System.Collections.Generic;
using System.Text;


namespace Common
{
    public enum ErrorMarkSeverity
    {
        Note,
        Warning,
        Error,
    }

    public interface IErrorMark
    {
        public string Message { get; }
        public long Begin { get; set; }
        public long End { get; set; }
        public ErrorMarkSeverity Severity { get; }
        public string Source { get; }
        public long Middle => (Begin + End) / 2;

        public bool UpdateAfterDelete(long position, long count); // bool = is alive
        public bool UpdateAfterInsert(long position, long count); // bool = is alive

        public bool IsFixItAvailable(IEditorBuffer buffer);
        public bool FixIt(IEditorBuffer buffer); // returns true on success
    }
}
