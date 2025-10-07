using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
