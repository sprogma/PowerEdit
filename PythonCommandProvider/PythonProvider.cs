using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PythonCommandProvider
{
    public class PythonProvider : CommandProviderInterface.ICommandProvider
    {
        public (long, long, string) ExampleScript => (18, 19, "o = map(lambda x: x, d)");

        public PythonProvider()
        { }

        ~PythonProvider()
        { }

        public IEnumerable<string>? Execute(string command, string[] args)
        {
            string inputData = "import json\n" +
                               "o = d = [" + string.Join(',', args.Select(x => JsonSerializer.Serialize(x))) + "]\n";
            string inputCode = $"{inputData}\n{command}\n" +
                               "print(json.dumps(list(map(str, o))))";
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

                    output = process.StandardOutput.ReadToEnd().Replace("\r", null);
                    string error = process.StandardError.ReadToEnd().Replace("\r", null);

                    process.WaitForExit();

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
                return null;
            }

            string[]? resultArray = JsonSerializer.Deserialize<string[]>(output);

            return resultArray;
        }
    }
}
