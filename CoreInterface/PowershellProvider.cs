using RegexTokenizer;
using Rope;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;

namespace PowershellCommandProvider
{
    public class PowershellProvider : CommandProviderInterface.ICommandProvider
    {
        public (long, long, string) ExampleScript => (13, 15, "$input | % { $_ }");
        public BaseTokenizer Tokenizer => new PowershellTokenizer();


        internal Runspace runSpace;

        public PowershellProvider()
        {
            runSpace = RunspaceFactory.CreateRunspace();
            runSpace.Open();
        }

        ~PowershellProvider()
        {
            runSpace.Close();
            runSpace.Dispose();
        }

        public (IEnumerable<string>?, string?) Execute(string command, string[] args)
        {
            Pipeline pipeline = runSpace.CreatePipeline();
            pipeline.Commands.AddScript(command);
            Collection<PSObject> results;
            try
            {
                results = pipeline.Invoke(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex}");
                return (null, ex.ToString());
            }
            return (results.Where(x => x != null).Select(x => x.ToString()), null);
        }
    }
}
