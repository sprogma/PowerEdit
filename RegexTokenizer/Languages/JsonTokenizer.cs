using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace RegexTokenizer.Languages;

[Language(["json"])]
public partial class JsonTokenizer : RegexTokenizer
{
    public JsonTokenizer() : base([
                    ("\"", "\"", "\\", false, TokenType.String),
                ], ParsingRegex()
    )
    { }


    [GeneratedRegex(@"(?<FloatLiteral>[+-]?(?:(?:0[xX](?:\.[0-9a-fA-F](?:_?[0-9a-fA-F])+|[0-9a-fA-F](?:_?[0-9a-fA-F])*\.?[0-9a-fA-F]*(?:_?[0-9a-fA-F])*)[pP][+-]?[0-9]+)|(?:\.[0-9](?:_?[0-9])+|[0-9](?:_?[0-9])*\.?[0-9]*(?:_?[0-9])*)[eE][+-]?[0-9]+|(?:\.[0-9](?:_?[0-9])+|[0-9](?:_?[0-9])*\.[0-9]*(?:_?[0-9])*)|[0-9](?:_?[0-9])+|[iI][nN][fF](?:[iI][nN][iI][tT][yY])?|[nN][aA][nN])[fFdDlLmM]?)
                         |(?<IntegerLiteral>\d+)
                         |(?<Operator>:|,)
                         |(?<OpenBraceCurl>\{)
                         |(?<CloseBraceCurl>\})
                         |(?<OpenBraceSquare>\[)
                         |(?<CloseBraceSquare>\])
                         ", RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)]

    private static partial Regex ParsingRegex();
}

