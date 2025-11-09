using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RegexTokenizer
{
    internal static class TokenizerExternsions
    {
        public static bool StartsWith(this string s, int from, string prefix)
        {
            return string.Compare(s, from, prefix, 0, prefix.Length, StringComparison.Ordinal) == 0;
        }
    }
}
