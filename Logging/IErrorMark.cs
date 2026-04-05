using System;
using System.Collections.Generic;
using System.Text;

namespace Common
{
    public enum ErrorMarkSeverity
    {
        Note,
        Waring,
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
    }
}
