using System.Diagnostics;
using System.Text;

namespace TestLens.Runner;

/// <summary>
/// Result of a single dotnet test invocation.
/// </summary>
public sealed record TestRunResult(
    int ExitCode,
    string Output,
    TimeSpan Elapsed);

/// <summary>
/// Runs <c>dotnet test</c> as a subprocess, capturing all stdout/stderr.
/// </summary>
public static class DotnetTestRunner
{
    /// <summary>
    /// Runs <c>dotnet test</c> with the given passthrough arguments plus injected
    /// TRX logger and results-directory arguments.
    /// </summary>
    public static async Task<TestRunResult> RunAsync(
        string[] passthroughArgs,
        string resultsDirectory,
        CancellationToken ct = default)
    {
        var args = new List<string> { "test" };
        args.AddRange(passthroughArgs);

        // Inject TRX logger (unless the user already specified one).
        if (!passthroughArgs.Any(a => a.Contains("--logger", StringComparison.OrdinalIgnoreCase)
                                   || a.Contains("-l", StringComparison.Ordinal)))
        {
            args.Add("--logger");
            args.Add("trx");
        }

        // Inject results directory.
        if (!passthroughArgs.Any(a => a.Contains("--results-directory", StringComparison.OrdinalIgnoreCase)))
        {
            args.Add("--results-directory");
            args.Add(resultsDirectory);
        }

        var sw = Stopwatch.StartNew();
        var (exitCode, output) = await RunCapturedAsync("dotnet", args, ct);
        return new TestRunResult(exitCode, output, sw.Elapsed);
    }

    private static async Task<(int ExitCode, string Output)> RunCapturedAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };

        var sb = new StringBuilder();
        var syncObj = new object();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (syncObj) sb.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (syncObj) sb.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        return (process.ExitCode, sb.ToString());
    }
}
