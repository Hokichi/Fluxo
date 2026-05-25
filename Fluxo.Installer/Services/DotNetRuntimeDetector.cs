using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Fluxo.Installer.Services;

public interface IDotNetRuntimeDetector
{
    bool IsRequiredRuntimeInstalled();
}

public sealed class DotNetRuntimeDetector : IDotNetRuntimeDetector
{
    private const string RuntimeName = "Microsoft.WindowsDesktop.App";
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(5);
    private readonly int requiredMajorVersion;
    private readonly Func<string?> runtimeListProvider;

    public DotNetRuntimeDetector(int requiredMajorVersion = 10, Func<string?>? runtimeListProvider = null)
    {
        this.requiredMajorVersion = requiredMajorVersion;
        this.runtimeListProvider = runtimeListProvider ?? GetRuntimeListFromDotNet;
    }

    public bool IsRequiredRuntimeInstalled()
    {
        string runtimeList;
        try
        {
            runtimeList = runtimeListProvider() ?? string.Empty;
        }
        catch
        {
            return false;
        }

        using var reader = new StringReader(runtimeList);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!line.StartsWith(RuntimeName + " ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
            {
                continue;
            }

            if (!Version.TryParse(tokens[1], out var version))
            {
                continue;
            }

            if (version.Major >= requiredMajorVersion)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetRuntimeListFromDotNet()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--list-runtimes",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();

            var stdOutReadTask = process.StandardOutput.ReadToEndAsync();
            var stdErrReadTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return string.Empty;
            }

            if (!Task.WaitAll([stdOutReadTask, stdErrReadTask], (int)ProcessTimeout.TotalMilliseconds))
            {
                return string.Empty;
            }

            return process.ExitCode == 0 ? stdOutReadTask.Result : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
