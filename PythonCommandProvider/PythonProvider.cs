using RegexTokenizer;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PythonCommandProvider
{
    public class PythonProvider : CommandProviderInterface.ICommandProvider
    {
        public BaseTokenizer Tokenizer => new PythonTokenizer();

        public PythonProvider()
        { }

        ~PythonProvider()
        { }

        public (long, long, string) ExampleScript(string editType)
        {
            return editType switch
            {
                "edit" => (23, 24, "output = map(lambda x: x, data)"),
                "replace" => (34, 34, "output = map(lambda x: x.replace(\"\",\"\"), data)"),
                _ => (0, 0, "")
            };
        }

        public (IEnumerable<object>?, string?) Execute(string command, object[] args)
        {
            string inputData = "import json\n" +
                               "output = data = [" + string.Join(',', args.Select(x => JsonSerializer.Serialize(x.ToString()))) + "]\n";
            string inputCode = $"{inputData}\n{command}\n" +
                               "print(json.dumps(list(map(str, output))))";

            string? error = null;
            string? output = null;

            Console.WriteLine(inputCode);

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "python3" : "py.exe"),
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    process.StandardInput.Write(inputCode);
                    process.StandardInput.Close();

                    if (process.WaitForExit(5000))
                    {
                        error = "Process don't stopped";
                        output = "ERROR";
                        process.Kill();
                    }
                    else
                    {
                        output = process.StandardOutput.ReadToEnd().Replace("\r", null);
                        error = process.StandardError.ReadToEnd().Replace("\r", null);
                    }

                    Console.WriteLine("Executable Output:");
                    Console.WriteLine(output);
                    Console.WriteLine("Executable Error:");
                    Console.WriteLine(error);
                    Console.WriteLine($"Exit Code: {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            if (output == null)
            {
                return (null, error);
            }

            string[]? resultArray;

            try
            {
                resultArray = JsonSerializer.Deserialize<string[]>(output);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return (null, error + "\n" + ex.ToString());
            }
            return (resultArray, null);
        }
    }
}
