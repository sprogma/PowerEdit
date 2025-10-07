using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandProviderInterface
{
    public interface ICommandProvider
    {
        public (long, long, string) ExampleScript(string editType);

        public BaseTokenizer Tokenizer { get; }

        public (IEnumerable<object>?, string?) Execute(string command, object[] args);
    }
}
