using Rope;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace PowershellProvider
{
    public class PowershellProvider : CommandProviderInterface.ICommandProvider
    {
        internal PowerShell ps;

        public PowershellProvider()
        {
            ps = PowerShell.Create();
        }

        public IEnumerable<Rope<char>> Execute(string command, Rope<char>[] args)
        {
            ps.AddScript(command);

            Collection<PSObject> results = ps.Invoke(args);
            Console.WriteLine($"GET: {results}");
            foreach (var item in results.Select(x => x.ToString()))
            {
                Console.WriteLine($"item: {item}");
            }

            return results.Select(x => x.ToString()).Select(x => (Rope<char>)x);
        }
    }
}
