using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RegexTokenizer
{
    public partial class PythonTokenizer: BaseTokenizer
    {
        public override List<Token> ParseContent(string content)
        {
            List<Token> result = [];
            /* 1. first parse comments and strings using loop */
            int pos = 0;
            while (pos < content.Length)
            {
                int end;
                if (content.Substring(pos).StartsWith("#"))
                {
                    end = content.IndexOf('\n', pos);
                    if (end == -1) { end = content.Length; }

                    result.Add(new Token(TokenType.Comment, pos, end));
                    pos = end + 1;
                }
                else if (content.Substring(pos).StartsWith("'''"))
                {
                    end = content.IndexOf("'''", pos + 1);
                    if (end == -1) { end = content.Length; }

                    while (end >= 1 &&
                           content[end - 1] == '\\')
                    {
                        end = content.IndexOf("'''", end + 1);
                        if (end == -1) { end = content.Length; break; }
                    }

                    result.Add(new Token(TokenType.String, pos, end + 2));
                    pos = end + 3;
                }
                else if (content.Substring(pos).StartsWith("\"\"\""))
                {
                    end = content.IndexOf("\"\"\"", pos + 1);
                    if (end == -1) { end = content.Length; }

                    while (end >= 1 &&
                           content[end - 1] == '\\')
                    {
                        end = content.IndexOf("\"\"\"", end + 1);
                        if (end == -1) { end = content.Length; break; }
                    }

                    result.Add(new Token(TokenType.String, pos, end + 2));
                    pos = end + 3;
                }
                else if (content.Substring(pos).StartsWith("'"))
                {
                    end = content.IndexOf('\'', pos + 1);
                    if (end == -1) { end = content.Length; }

                    while (end >= 1 &&
                           content[end - 1] == '\\')
                    {
                        end = content.IndexOf('\'', end + 1);
                        if (end == -1) { end = content.Length; break; }
                    }

                    result.Add(new Token(TokenType.String, pos, end));
                    pos = end + 1;
                }
                else if (content.Substring(pos).StartsWith("\""))
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
                    pos = end + 1;
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
                int end = content.IndexOf('\n', pos + 1);
                if (end == -1)
                {
                    end = content.Length;
                }

                string lineSlice = content.Substring(pos, end - pos);
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
                        regexResult.Add(new Token(TokenType.Variable, pos + m.Index, pos + m.Index + m.Length - 1));
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


        [GeneratedRegex(@"(?<key>\b(if|elif|else|for|while|continue|break|return|yield|from|import|assert|try|except|finally|def|class|global|nonlocal|match|case|async|await|with|and|or|in|not|is|as|lambda|del|False|True|None|pass|raise)\b)|(?<func>\b(\w|[_$])(\w|\d|[_$])*(?=\s*\())|(?<type>((?<=\bclass\s+)(\w|[_$])(\w|\d|[_$])*\b))|(?<var>\b[_$\w-[0-9]](\w|[_$])*\b)|(?<float>(\d*\.\d+|\d+\.\d*)([eE][+\-]\d+)?)|(?<int>(0[xX]?)?\d+)|(?<operator>[#!,.\-+*/?;:|&~<=>(){}\[\]])")]

        private static partial Regex OtherComponentsRegex();
    }
}
