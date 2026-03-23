using RegexTokenizer;
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


        internal RunspacePool? runSpacePool;

        public PowershellProvider()
        {
            Task.Run(() =>
            {
                runSpacePool = RunspaceFactory.CreateRunspacePool(1, 1);
                runSpacePool.Open();
            });
        }

        ~PowershellProvider()
        {
            runSpacePool?.Close();
            runSpacePool?.Dispose();
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
            if (runSpacePool == null)
            {
                return (null, "runspace don't initializated yet");
            }
            using PowerShell ps = PowerShell.Create();
            ps.RunspacePool = runSpacePool;
            ps.AddScript(command);
            try
            {
                var results = ps.Invoke(args);
                if (ps.HadErrors)
                {
                    var errorMsg = string.Join(Environment.NewLine, ps.Streams.Error);
                    return (null, errorMsg);
                }
                return (results.Select(x => x?.BaseObject ?? x).OfType<object>(), null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex}");
                return (null, ex.ToString());
            }
        }
    }
}
