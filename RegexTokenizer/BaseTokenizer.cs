using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace RegexTokenizer
{
    public abstract class BaseTokenizer
    {
        public abstract List<Token> ParseContent(string content);

        public static BaseTokenizer CreateTokenizer(string fileExternsion)
        {
            Console.WriteLine($"Creating ... {fileExternsion} tokenizer");
            switch (fileExternsion)
            {
                case "c":
                case "h":
                    return new CTokenizer();
                case "cpp":
                case "hpp":
                    return new CTokenizer();
                case "py":
                    return new PythonTokenizer();
                case "ps1":
                case "psm1":
                case "psd1":
                    return new PowershellTokenizer();
                default:
                    break;
            }
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
    }
}
