using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;

namespace Nuke.GlobalTool
{
    public class Program
    {
        private static string ScriptHost => EnvironmentInfo.IsWin ? "powershell" : "bash";
        private static string ScriptExtension => EnvironmentInfo.IsWin ? "ps1" : "sh";
        private static string SetupScriptName => $"setup.{ScriptExtension}";
        private static string BuildScriptName => $"build.{ScriptExtension}";

        private static void Main(string[] args)
        {
            if (args.Length == 0 || !args[0].StartsWith("!"))
            {
                var buildScript = FileSystemTasks.FindParentDirectory(
                        new DirectoryInfo(Directory.GetCurrentDirectory()),
                        x => x.GetFiles(NukeBuild.ConfigurationFile).Any())
                    ?.GetFiles(BuildScriptName, SearchOption.AllDirectories)
                    .FirstOrDefault()?.FullName;
                if (buildScript != null)
                {
                    // TODO: docker

                    RunScript(buildScript, args.Select(x => x.DoubleQuoteIfNeeded()).JoinSpace());
                    return;
                }
                
                ConsoleKey response;
                do
                {
                    Logger.Log($"Could not find {NukeBuild.ConfigurationFile} file. Do you want to setup a build? [y/n]");
                    response = Console.ReadKey(intercept: true).Key;
                } while (response != ConsoleKey.Y && response != ConsoleKey.N);

                if (response == ConsoleKey.N)
                    return;
            }
            
            // TODO: embed all bootstrapping files and reimplement setup for offline usage?
            // TODO: alternatively, use a similar approach as in SetupIntegrationTest
            var setupScript = Path.Combine(Directory.GetCurrentDirectory(), SetupScriptName);
            if (!File.Exists(setupScript))
            {
                using (var webClient = new WebClient())
                {
                    var setupScriptUrl = $"https://nuke.build/{ScriptHost}";
                    Logger.Log($"Downloading setup script from {setupScriptUrl}");
                    webClient.DownloadFile(setupScriptUrl, setupScript);
                }
            }

            RunScript(setupScript);
        }

        private static void RunScript(string file, string arguments = null)
        {
            var process = ProcessTasks.StartProcess(ScriptHost, $"-File {file.DoubleQuoteIfNeeded()} {arguments}");
            if (process == null || !process.WaitForExit() || process.ExitCode != 0)
                Environment.Exit(exitCode: 0);
        }
    }
}
