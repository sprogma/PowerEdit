using Common;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace RegexTokenizer
{
    using TypeEntry = (string begin, string? end, string? escapeSeq, bool multiline, TokenType token, SearchValues<string> endSearcher);

    public class RegexTokenizer : BaseTokenizer
    {
        public TypeEntry[] BoundedTypes { get; init; }

        private SearchValues<string> FeatureStartSearcher;

        internal Regex LineTokenizer;

        private readonly (int Id, TokenType Type)[] GroupMap;


        public RegexTokenizer((string begin, string? end, string? escapeSeq, bool multiline, TokenType token)[] boundedTypes,
                              Regex regex)
        {
            BoundedTypes = boundedTypes.Select(x => (x.begin, x.end, x.escapeSeq, x.multiline, x.token, SearchValues.Create(x.end != null ? [x.end, "\n"] : ["\n"], StringComparison.Ordinal))).ToArray();
            FeatureStartSearcher = SearchValues.Create(boundedTypes.Select(x => x.begin).ToArray(), StringComparison.Ordinal);
            LineTokenizer = regex;

            var tokenDefinitions = Enum.GetValues<TokenType>().ToDictionary(type => type.ToString(), type => type);
            GroupMap = tokenDefinitions.Select(d => (Id: LineTokenizer.GroupNumberFromName(d.Key), d.Value))
                                       .Where(x => x.Id >= 0)
                                       .ToArray();
        }

        public override List<Token> ParseContent(string content)
        {
            List<Token> result = [];
            var span = content.AsSpan();

            /* 1. first parse comments and strings using loop */
            int pos = 0, ppos = -1;
            while (pos < span.Length)
            {
                // find feature
                int begin = span[pos..].IndexOfAny(FeatureStartSearcher);
                if (pos == ppos)
                {
                    Logger.Log(LogLevel.Error, $"RegexTokenizer step was empty at {pos}.");
                    break;
                }
                ppos = pos;
                if (begin == -1)
                {
                    break;
                }
                begin += pos;
                // find what it is
                var beginSpan = span.Slice(begin);
                int end = -1;
                foreach (var comment in BoundedTypes)
                {   
                    if (beginSpan.StartsWith(comment.begin))
                    {
                        pos = begin + comment.begin.Length;

                        do
                        {
                            if (pos >= span.Length)
                            {
                                end = -1;
                                break;
                            }

                            end = span[pos..].IndexOfAny(comment.endSearcher);

                            if (end == -1)
                            {
                                end = span.Length;
                            }
                            else if (comment.end == null || span[end] == '\n')
                            {
                                end = pos + end + 1;
                            }
                            else
                            {
                                end += pos;
                                if (comment.escapeSeq != null && end != 0 && span[..(end - 1)].EndsWith(comment.escapeSeq))
                                {
                                    pos = end + 1; // skip this position
                                    continue;
                                }
                                end += comment.end.Length;
                            }
                        }
                        while (false);

                        if (end == -1)
                        {
                            end = span.Length;
                        }

                        result.Add(new Token(comment.token, begin, end));
                        pos = end;
                    }
                }
            }


            /* 2. parse all rest lines using regular expressions */
            List<Token> regexResult = [];
            pos = 0;
            while (pos < content.Length)
            {
                int nextNewLine = content.IndexOf('\n', pos);
                int end = (nextNewLine == -1) ? content.Length : nextNewLine;
                foreach (Match m in LineTokenizer.Matches(content[pos..end]))
                {
                    foreach (var entry in GroupMap)
                    {
                        if (m.Groups[entry.Id].Success)
                        {
                            regexResult.Add(new Token(entry.Type, pos + m.Index, pos + m.Index + m.Length - 1));
                            break;
                        }
                    }
                }
                pos = end + 1;
            }


            /* 3. merge both arrays to result */
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

            return UpdateTokensAsUTF8(content, result);
        }
    }
}
