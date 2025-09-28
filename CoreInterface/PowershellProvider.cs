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

        public (long, long, string) ExampleScript(string editType)
        {
            return editType switch
            {
                "edit" => (16, 18, "@($input) | % { $_ }"),
                "replace" => (19, 19, "@($input) -replace\"\",{\"\"}"),
                "powerEdit" => (9, 9, "@($input)"),
                _ => (0, 0, "")
            };
        }

        public (IEnumerable<object>?, string?) Execute(string command, object[] args)
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
            return (results.Select(x => (x is PSObject o ? o.BaseObject: x)).Where(x => x != null), null);
        }
    }
}
