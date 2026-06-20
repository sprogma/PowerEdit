using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RegexTokenizer.Languages;

[Language(["python"])]
public partial class PythonTokenizer : RegexTokenizer
{
    public PythonTokenizer() : base([
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
                    ("#", "\n", "\\", true, TokenType.Comment),
                ], ParsingRegex()
    )
    { }


    [GeneratedRegex(@"(?<Keyword>\b(if|elif|else|for|while|continue|break|return|yield|from|import|assert|try|except|finally|def|class|global|nonlocal|match|case|async|await|with|and|or|in|not|is|as|lambda|del|False|True|None|pass|raise)\b)
                         |(?<Function>\b(\w|[_$])(\w|\d|[_$])*(?=\s*\())
                         |(?<Class>((?<=\bclass\s+)(\w|[_$])(\w|\d|[_$])*\b))
                         |(?<Variable>\b[_$\w-[0-9]](\w|[_$])*\b)
                         |(?<FloatLiteral>[+-]?(?:(?:0[xX](?:\.[0-9a-fA-F](?:_?[0-9a-fA-F])+|[0-9a-fA-F](?:_?[0-9a-fA-F])*\.?[0-9a-fA-F]*(?:_?[0-9a-fA-F])*)[pP][+-]?[0-9]+)|(?:\.[0-9](?:_?[0-9])+|[0-9](?:_?[0-9])*\.?[0-9]*(?:_?[0-9])*)[eE][+-]?[0-9]+|(?:\.[0-9](?:_?[0-9])+|[0-9](?:_?[0-9])*\.[0-9]*(?:_?[0-9])*)|[0-9](?:_?[0-9])+|[iI][nN][fF](?:[iI][nN][iI][tT][yY])?|[nN][aA][nN])[fFdDlLmM]?)
                         |(?<IntegerLiteral>(0[xX][0-9a-fA-F_]+)|(0[bB][01_]+)|(0[oO][0-7_]+)|[\d_]+)
                         |(?<Operator>[#!,.\-+*/?;:|&~<=>])|
                         |(?<OpenBraceRound>\()
                         |(?<CloseBraceRound>\))
                         |(?<OpenBraceCurl>\{)
                         |(?<CloseBraceCurl>\})
                         |(?<OpenBraceSquare>\[)
                         |(?<CloseBraceSquare>\])", RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)]

    private static partial Regex ParsingRegex();
}
