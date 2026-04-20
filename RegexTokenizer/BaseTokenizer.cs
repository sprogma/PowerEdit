using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RegexTokenizer
{
    public abstract partial class BaseTokenizer
    {
        public abstract List<Token> ParseContent(string content);

        public virtual long MaxContentSize => 256*1024;

        public static BaseTokenizer CreateTokenizer(string? languageId)
        {
            Logger.Log($"Creating ... {languageId} tokenizer");
            switch (languageId)
            {
                case "c":
                    return new CTokenizer();
                case "cpp":
                    return new CTokenizer();
                case "python":
                    return new RegexTokenizer([
                            ("r\"", "\"", null, false, TokenType.RawString),
                            ("r\'", "\'", null, false, TokenType.RawString),
                            ("r\"\"\"", "\"\"\"", null, true, TokenType.RawString),
                            ("r\'\'\'", "\'\'\'", null, true, TokenType.RawString),
                            ("f\"", "\"", "\\", false, TokenType.FormatString),
                            ("f\'", "\'", "\\", false, TokenType.FormatString),
                            ("f\"\"\"", "\"\"\"", "\\", true, TokenType.FormatString),
                            ("f\'\'\'", "\'\'\'", "\\", true, TokenType.FormatString),
                            ("\"", "\"", "\\", false, TokenType.String),
                            ("\'", "\'", "\\", false, TokenType.String),
                            ("\"\"\"", "\"\"\"", "\\", true, TokenType.String),
                            ("\'\'\'", "\'\'\'", "\\", true, TokenType.String),
                        ], PythonRegex());
                case "powershell":
                    return new PowershellTokenizer();
                case "json": return new RegexTokenizer(
                        [
                            ("\"", "\"", "\\", false, TokenType.String),
                        ], JsonRegex());
                default:
                    break;
            }
            return new SimpleTokenizer();
        }

        public static BaseTokenizer CreateBaseTokenizer()
        {
            return new SimpleTokenizer();
        }

        static public List<Token> UpdateTokensAsUTF8(string input, List<Token> tokens)
        {
            int currentUtf16Pos = 0;
            long currentUtf8BytePos = 0;
            var encoding = System.Text.Encoding.UTF8;

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                int diffBegin = (int)token.begin - currentUtf16Pos;
                if (diffBegin > 0)
                {
                    currentUtf8BytePos += encoding.GetByteCount(input, currentUtf16Pos, diffBegin);
                    currentUtf16Pos = (int)token.begin;
                }
                long utf8Begin = currentUtf8BytePos;
                int diffEnd = (int)token.end - currentUtf16Pos;
                if (diffEnd > 0)
                {
                    currentUtf8BytePos += encoding.GetByteCount(input, currentUtf16Pos, diffEnd);
                    currentUtf16Pos = (int)token.end;
                }
                long utf8End = currentUtf8BytePos;
                tokens[i] = new Token(token.type, utf8Begin, utf8End);
            }
            return tokens;
        }

        /*
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
        Macro,
        Function,
        Class,
        Keyword,
        Operator,
        NameHint,
        OpenBraceRound,
        CloseBraceRound,
        OpenBraceSquare,
        CloseBraceSquare,
        OpenBraceCurl,
        CloseBraceCurl,
         */

        [GeneratedRegex(@"(?<FloatLiteral>\d+.\d*(e[+-]?\d+)?
                                         |\d*.\d+(e[+-]?\d+)?
                                         |\d+(e[+-]?\d+))
                         |(?<IntegerLiteral>\d+)
                         |(?<Operator>:|,)
                         |(?<OpenBraceCurl>\{)
                         |(?<CloseBraceCurl>\})
                         |(?<OpenBraceSquare>\[)
                         |(?<CloseBraceSquare>\])
                         ", RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)]
        private static partial Regex JsonRegex();


        [GeneratedRegex(@"(?<Keyword>\b(if|elif|else|for|while|continue|break|return|yield|from|import|assert|try|except|finally|def|class|global|nonlocal|match|case|async|await|with|and|or|in|not|is|as|lambda|del|False|True|None|pass|raise)\b)
                         |(?<Function>\b(\w|[_$])(\w|\d|[_$])*(?=\s*\())
                         |(?<Class>((?<=\bclass\s+)(\w|[_$])(\w|\d|[_$])*\b))
                         |(?<Variable>\b[_$\w-[0-9]](\w|[_$])*\b)
                         |(?<FloatLiteral>\d+.\d*(e[+-]?\d+)?
                                         |\d*.\d+(e[+-]?\d+)?
                                         |\d+(e[+-]?\d+))
                         |(?<IntegerLiteral>(0[xX][0-9a-fA-F_]+)|(0[bB][01_]+)|(0[oO][0-7_]+)|[\d_]+)
                         |(?<Operator>[#!,.\-+*/?;:|&~<=>])|
                         |(?<OpenBraceRound>\()
                         |(?<CloseBraceRound>\))
                         |(?<OpenBraceCurl>\{)
                         |(?<CloseBraceCurl>\})
                         |(?<OpenBraceSquare>\[)
                         |(?<CloseBraceSquare>\])", RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)]

        private static partial Regex PythonRegex();
    }
}
