using Rope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegexTokenizer
{
    public class SimpleTokenizer : BaseTokenizer
    {
        public override List<Token> ParseContent(Rope<char> content)
        {
            return [];
        }
    }
}
