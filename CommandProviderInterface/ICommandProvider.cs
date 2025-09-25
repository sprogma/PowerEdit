using RegexTokenizer;
using Rope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandProviderInterface
{
    public interface ICommandProvider
    {
        public (long, long, string) ExampleScript { get; }

        public virtual BaseTokenizer Tokenizer => new SimpleTokenizer();

        public (IEnumerable<string>?, string?) Execute(string command, string[] args);
    }
}
