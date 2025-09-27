using Rope;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace RegexTokenizer
{
    public partial class CTokenizer : BaseTokenizer
    {
        public override List<Token> ParseContent(Rope<char> content)
        {
            List<Token> result = [];
            /* 1. first parse comments and strings using loop */
            long pos = 0;
            while (pos < content.Length)
            {
                long end;
                if (content.Slice(pos).StartsWith("//"))
                {
                    end = content.IndexOf('\n', pos);
                    if (end == -1) { end = content.Length; }

                    while (( end >= 1 &&
                             content[end - 1] == '\\' ) ||
                           ( end >= 2 &&
                             content[end - 1] == '\r' &&
                             content[end - 2] == '\\' ))
                    {
                        end = content.IndexOf('\n', end + 1);
                        if (end == -1) { end = content.Length; break; }
                    }
                    result.Add(new Token(TokenType.Comment, pos, end));
                    pos = end + 1;
                }
                else if (content.Slice(pos).StartsWith("/*"))
                {
                    end = content.IndexOf("*/", pos + 2);
                    if (end == -1) { end = content.Length; }

                    result.Add(new Token(TokenType.MultilineComment, pos, end + 1));
                    pos = end + 2;
                }
                else if (content.Slice(pos).StartsWith("'"))
                {
                    end = content.IndexOf('\'', pos + 1);
                    if (end == -1) { end = content.Length; }

                    while (end >= 1 &&
                           content[end - 1] == '\\')
                    {
                        end = content.IndexOf('\'', end + 1);
                        if (end == -1) { end = content.Length; break; }
                    }

                    result.Add(new Token(TokenType.Char, pos, end));
                    pos = end + 2;
                }
                else if (content.Slice(pos).StartsWith("\""))
                {
                    end = content.IndexOf('"', pos + 1);
                    if (end == -1) { end = content.Length; }

                    while (end >= 1 &&
                           content[end - 1] == '\\')
                    {
                        end = content.IndexOf('"', end + 1);
                        if (end == -1) { end = content.Length; break; }
                    }

                    result.Add(new Token(TokenType.String, pos, end));
                    pos = end + 2;
                }
                else if (content.Slice(pos).StartsWith("R\""))
                {
                    string beginString = content.Slice(pos, Math.Min(content.Length - pos, 64)).ToString();
                    Match match = RStringRegex().Match(beginString);
                    if (match.Success == false)
                    {
                        pos += 2;
                    }
                    else
                    {
                        string name = match.Groups[1].Value;

                        end = content.IndexOf($"){name}\"", pos + 2 + name.Length + 1);
                        if (end == -1) { end = content.Length; }

                        result.Add(new Token(TokenType.RawString, pos, end + 1 + name.Length + 1));
                        pos = end + 1 + name.Length + 1 + 1;
                    }
                }
                else
                {
                    pos++;
                }
            }


            /* 2. parse all rest lines using regular expressions */
            List<Token> regexResult = [];
            pos = 0;
            while (pos < content.Length)
            {
                long end = content.IndexOf('\n', pos + 1);
                if (end == -1)
                {
                    end = content.Length;
                }

                Rope<char> lineSlice = content.Slice(pos, end - pos);
                string line = lineSlice.ToString();

                MatchCollection res = OtherComponentsRegex().Matches(line);
                foreach (Match m in res)
                {
                    if (m.Groups["key"].Success)
                    {
                        regexResult.Add(new Token(TokenType.Keyword, pos + m.Index, pos + m.Index + m.Length - 1));
                    }
                    if (m.Groups["type"].Success)
                    {
                        regexResult.Add(new Token(TokenType.Class, pos + m.Index, pos + m.Index + m.Length - 1));
                    }
                    if (m.Groups["func"].Success)
                    {
                        regexResult.Add(new Token(TokenType.Function, pos + m.Index, pos + m.Index + m.Length - 1));
                    }
                    if (m.Groups["var"].Success)
                    {
                        if (m.Value.EndsWith("_t"))
                        {   
                            regexResult.Add(new Token(TokenType.Class, pos + m.Index, pos + m.Index + m.Length - 1));
                        }
                        else
                        {
                            regexResult.Add(new Token(TokenType.Variable, pos + m.Index, pos + m.Index + m.Length - 1));
                        }
                    }
                    if (m.Groups["float"].Success)
                    {
                        regexResult.Add(new Token(TokenType.FloatLiteral, pos + m.Index, pos + m.Index + m.Length - 1));
                    }
                    if (m.Groups["int"].Success)
                    {
                        regexResult.Add(new Token(TokenType.IntegerLiteral, pos + m.Index, pos + m.Index + m.Length - 1));
                    }
                    if (m.Groups["operator"].Success)
                    {
                        regexResult.Add(new Token(TokenType.Operator, pos + m.Index, pos + m.Index + m.Length - 1));
                    }
                }

                pos = end;
            }

            {
                long lastResult = 0;
                long regexPos = 0;

                List<Token> filtered = [];

                while (lastResult < result.Count && regexPos < regexResult.Count)
                {
                    if (regexResult[(int)regexPos].begin > result[(int)lastResult].end)
                    {
                        lastResult++;
                    }
                    /* assert (regexResult[(int)regexPos].begin < result[(int)lastResult].end) */
                    else if (regexResult[(int)regexPos].end < result[(int)lastResult].begin)
                    {
                        filtered.Add(regexResult[(int)regexPos]);
                        regexPos++;
                    }
                    else
                    {
                        regexPos++;
                    }
                }
                while (regexPos < regexResult.Count)
                {
                    filtered.Add(regexResult[(int)regexPos]);
                    regexPos++;
                }

                result.AddRange(filtered);
            }

            result.Sort((x, y) => x.begin.CompareTo(y.begin));

            return result;
        }

        [GeneratedRegex(@"^R""([^(]*)\(")]
        private static partial Regex RStringRegex();

        [GeneratedRegex(@"(?<key>\b(define|include|pragma|error|warning|if|else|for|while|do|goto|return|continue|break|typedef|struct|sizeof|volatile|__volatile__|asm|__asm__|inline|__inline__|register|__register__|restrict|static|extern|const)\b)|(?<func>\b(\w|[_$])(\w|\d|[_$])*(?=\s*\())|(?<type>((?<=\bstruct\s+)(\w|[_$])(\w|\d|[_$])*\b|\b([_$\w-[0-9]])(\w|\d|[_$])*(?=\s+[_$\w-[0-9]])))|(?<var>\b[_$\w-[0-9]](\w|[_$])*\b)|(?<float>(\d*\.\d+|\d+\.\d*)([eE][+\-]\d+)?([lL]|[fF])?)|(?<int>(0[xX]?)?\d+([zZ]|[uU][lL][lL]|[uU][lL]|[uU]|([lL]?)([lL]?)([uU]?))?)|(?<operator>[#!,.\-+*/?;:|&~<=>(){}\[\]])")]

        private static partial Regex OtherComponentsRegex();
    }
}
