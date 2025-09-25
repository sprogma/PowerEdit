using Rope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegexTokenizer
{
    public abstract class BaseTokenizer
    {
        public abstract List<Token> ParseContent(Rope<char> content);

        public static BaseTokenizer CreateTokenizer(string fileExternsion)
        {
            Console.WriteLine($"Creating ... {fileExternsion} tokenizer");
            switch (fileExternsion)
            {
                case "c":
                    return new CTokenizer();
                default:
                    break;
            }
            return new SimpleTokenizer();
        }
    }
}
