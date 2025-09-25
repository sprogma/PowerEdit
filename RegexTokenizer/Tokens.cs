
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegexTokenizer
{
    public enum TokenType
    {
        Comment,
        MultilineComment,
        Char,
        String,
        RawString,
        FormatString,
        IntegerLiteral,
        FloatLiteral,
        Variable,
        Costant,
        Function,
        Class,
        Keyword,
        Operator,
        OpenBraceRound,
        CloseBraceRound,
        OpenBraceSquare,
        CloseBraceSquare,
        OpenBraceCurl,
        CloseBraceCurl,
    }

    public struct Token(TokenType type, long begin, long end)
    {
        public TokenType type = type;
        public long begin = begin;
        public long end = end;
    }
}
