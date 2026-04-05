using Common;
using RegexTokenizer;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PythonCommandProvider
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(string[]))]
    internal partial class PythonJsonContext : JsonSerializerContext
    {
    }

    public class PythonProvider : CommandProviderInterface.ICommandProvider
    {
        public BaseTokenizer Tokenizer => new PythonTokenizer();
        public string? LanguageId => "python";

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
                               "output = data = " + JsonSerializer.Serialize(args.Select(x => x.ToString()).ToArray(), PythonJsonContext.Default.StringArray) + "\n";
            string inputCode = $"{inputData}\n{command}\n" +
                               "print(json.dumps(list(map(str, output))))";

            string? error = null;
            string? output = null;

            Logger.Log($"Executing {inputCode}");

            try
            {
                ProcessStartInfo startInfo = new()
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
                        output = process.StandardOutput.ReadToEnd().Replace("\r", null);
                        error = process.StandardError.ReadToEnd().Replace("\r", null);
                    }
                    else
                    {
                        process.Kill();
                        return (null, "ERROR: Process don't completed in 5 seconds\n");
                    }

                    Logger.Log("Executable Output:");
                    Logger.Log(output);
                    Logger.Log("Executable Error:");
                    Logger.Log(error);
                    Logger.Log($"Exit Code: {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, $"An error occurred: {ex.Message}");
            }

            if (output == null)
            {
                return (null, error);
            }

            string[]? resultArray;

            try
            {
                resultArray = JsonSerializer.Deserialize(output, PythonJsonContext.Default.StringArray);
            }
            catch (Exception ex)
            {
                Logger.Log(ex.ToString());
                return (null, error + "\n" + ex.ToString());
            }
            return (resultArray, null);
        }
    }
}
