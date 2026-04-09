using System.Diagnostics;
using System.Text;

namespace TestLens.Runner;

public sealed record BuildResult(string ProjectPath, string ProjectName, int ExitCode, string Output, TimeSpan Elapsed);

public sealed record TestRunResult(string ProjectPath, string ProjectName, int ExitCode, string TrxPath, TimeSpan Elapsed);

/// <summary>
/// Thin wrapper around dotnet build / dotnet test subprocesses.
/// All subprocess stdout/stderr is captured (suppressed from the terminal); callers can
/// inspect it via the result records.
/// </summary>
public static class DotnetTestRunner
{
    // ── build ─────────────────────────────────────────────────────────────────

    public static async Task<BuildResult> BuildAsync(
        string projectPath,
        string configuration,
        bool noRestore,
        CancellationToken ct = default)
    {
        var args = new List<string> { "build", projectPath, "-c", configuration };
        if (noRestore) args.Add("--no-restore");

        var sw = Stopwatch.StartNew();
        var (exitCode, output) = await RunCapturedAsync("dotnet", args, ct);
        return new BuildResult(projectPath, ProjectName(projectPath), exitCode, output, sw.Elapsed);
    }

    // ── test ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs `dotnet test --no-build` with the TRX logger writing to <paramref name="trxPath"/>.
    /// Returns after the process exits; the TRX file is fully written at that point.
    /// </summary>
    public static async Task<TestRunResult> TestAsync(
        string projectPath,
        string trxPath,
        string? filter,
        CancellationToken ct = default)
    {
        var trxDir = Path.GetDirectoryName(trxPath)!;
        var trxFile = Path.GetFileName(trxPath);

        var args = new List<string>
        {
            "test", projectPath,
            "--no-build",
            "--logger", $"trx;LogFileName={trxFile}",
            "--results-directory", trxDir,
        };

        if (filter is not null)
        {
            args.Add("--filter");
            args.Add(filter);
        }

        var sw = Stopwatch.StartNew();
        var (exitCode, _) = await RunCapturedAsync("dotnet", args, ct);
        return new TestRunResult(projectPath, ProjectName(projectPath), exitCode, trxPath, sw.Elapsed);
    }

    // ── internals ─────────────────────────────────────────────────────────────

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

    public static string ProjectName(string csprojPath) =>
        Path.GetFileNameWithoutExtension(csprojPath);
}
