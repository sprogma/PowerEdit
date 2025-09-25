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

        public IEnumerable<string>? Execute(string command, string[] args);
    }
}
