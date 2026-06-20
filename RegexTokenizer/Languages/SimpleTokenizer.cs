using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RegexTokenizer.Languages;

public partial class SimpleTokenizer : RegexTokenizer
{
    public SimpleTokenizer() : base([
                    ("\"", "\"", "\\", false, TokenType.String),
                    ("#", "\n", "\\", true, TokenType.Comment),
                ], ParsingRegex()
    )
    { }


    [GeneratedRegex(@"(?<FloatLiteral>[+-]?(?:(?:0[xX](?:\.[0-9a-fA-F](?:_?[0-9a-fA-F])+|[0-9a-fA-F](?:_?[0-9a-fA-F])*\.?[0-9a-fA-F]*(?:_?[0-9a-fA-F])*)[pP][+-]?[0-9]+)|(?:\.[0-9](?:_?[0-9])+|[0-9](?:_?[0-9])*\.?[0-9]*(?:_?[0-9])*)[eE][+-]?[0-9]+|(?:\.[0-9](?:_?[0-9])+|[0-9](?:_?[0-9])*\.[0-9]*(?:_?[0-9])*)|[0-9](?:_?[0-9])+|[iI][nN][fF](?:[iI][nN][iI][tT][yY])?|[nN][aA][nN])[fFdDlLmM]?)
                         |(?<IntegerLiteral>(0[xX][0-9a-fA-F_]+)|(0[bB][01_]+)|(0[oO][0-7_]+)|[\d_]+)
                         |(?<Operator>[#!,.\-+*/?;:|&~<=>])|
                         |(?<OpenBraceRound>\()
                         |(?<CloseBraceRound>\))
                         |(?<OpenBraceCurl>\{)
                         |(?<CloseBraceCurl>\})
                         |(?<OpenBraceSquare>\[)
                         |(?<CloseBraceSquare>\])
                         ", RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)]

    private static partial Regex ParsingRegex();
}

