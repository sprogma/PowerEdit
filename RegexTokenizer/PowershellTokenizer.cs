using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RegexTokenizer
{
    public partial class PowershellTokenizer : BaseTokenizer
    {
        public override List<Token> ParseContent(string content)
        {
            List<Token> result = [];
            /* 1. first parse comments and strings using loop */
            int pos = 0;
            while (pos < content.Length)
            {
                int end;
                if (content.StartsWith(pos, "#"))
                {
                    end = content.IndexOf('\n', pos);
                    if (end == -1) { end = content.Length; }

                    while ((end >= 1 &&
                             content[end - 1] == '`') ||
                           (end >= 2 &&
                             content[end - 1] == '\r' &&
                             content[end - 2] == '`'))
                    {
                        end = content.IndexOf('\n', end + 1);
                        if (end == -1) { end = content.Length; break; }
                    }
                    result.Add(new Token(TokenType.Comment, pos, end));
                    pos = end + 1;
                }
                else if (content.StartsWith(pos, "<#"))
                {
                    end = content.IndexOf("#>", pos + 2);
                    if (end == -1) { end = content.Length; }

                    result.Add(new Token(TokenType.MultilineComment, pos, end + 1));
                    pos = end + 2;
                }
                else if (content.StartsWith(pos, "'"))
                {
                    end = content.IndexOf('\'', pos + 1);
                    if (end == -1) { end = content.Length; }

                    while (end >= 1 &&
                           content[end - 1] == '`')
                    {
                        end = content.IndexOf('\'', end + 1);
                        if (end == -1) { end = content.Length; break; }
                    }

                    result.Add(new Token(TokenType.RawString, pos, end));
                    pos = end + 1;
                }
                else if (content.StartsWith(pos, "@\'"))
                {
                    end = content.IndexOf("\'@", pos + 2);
                    if (end == -1) { end = content.Length; }

                    result.Add(new Token(TokenType.RawString, pos, end + 1));
                    pos = end + 2;
                }
                else if (content.StartsWith(pos, "\""))
                {
                    end = content.IndexOf('"', pos + 1);
                    if (end == -1) { end = content.Length; }

                    while (end >= 1 &&
                           content[end - 1] == '`')
                    {
                        end = content.IndexOf('"', end + 1);
                        if (end == -1) { end = content.Length; break; }
                    }

                    result.Add(new Token(TokenType.String, pos, end));
                    pos = end + 1;
                }
                else if (content.StartsWith(pos, "@\""))
                {
                    end = content.IndexOf("\"@", pos + 2);
                    if (end == -1) { end = content.Length; }

                    while (end >= 2 &&
                           content[end - 2] == '`')
                    {
                        end = content.IndexOf("\"@", end + 1);
                        if (end == -1) { end = content.Length; break; }
                    }

                    result.Add(new Token(TokenType.String, pos, end + 1));
                    pos = end + 2;
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
                    if (m.Groups["operator"].Success || m.Groups["namedoperator"].Success)
                    {
                        regexResult.Add(new Token(TokenType.Operator, pos + m.Index, pos + m.Index + m.Length - 1));
                    }
                    if (m.Groups["namehint"].Success)
                    {
                        regexResult.Add(new Token(TokenType.NameHint, pos + m.Index, pos + m.Index + m.Length - 1));
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


        [GeneratedRegex(@"(?<namedoperator>(-(and|or|xor|band|bnot|bor|bxor|shl|shr|replace|split|match|notmatch|lt|le|gt|ge|ne|eq|in|notin|contains|notcontains|not)))|(?<key>\b(param|class|function|for|foreach|foreach\s+parallel|while|do|until|if|elseif|else|begin|process|try|catch|switch|case|default|end|return|break|continue|exit|throw)\b)|(?<func>((\b(\w|[_$?])(\w|\d|[_$?])*(?=\s*\())|((?<=[\s|+*])(\w|[_$])(\w|\d|[_$])*(?=\s*\())|(?<=function)\s+[\w\-_$?]+))|(?<type>((?<=\bclass\s+)(\w|[_$])(\w|\d|[_$])*\b|\[[\w.]*?\]))|(?<namehint>(-[\w_$:?]+))|(?<var>\$[\w_$:?]+|\$\{[^}]*\})|(?<float>(\d*\.\d+|\d+\.\d*)([eE][+\-]\d+)?[d]?|\d+(([eE][+\-]\d+)[d]?|d))|(?<int>(0[xX]?)?\d+[lun]?)|(?<operator>[@#!,.\-+*/?;:|&~<=>(){}\[\]])", RegexOptions.IgnoreCase)]
        private static partial Regex OtherComponentsRegex();
    }
}
