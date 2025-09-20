using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandProviderInterface
{
    public interface ICommandProvider
    {
        public Rope.Rope<char> Execute(Rope.Rope<char> args);
    }
}
