using System.Diagnostics;
using Spectre.Console;
using TestLens.Display;
using TestLens.Trx;

namespace TestLens.Runner;

/// <summary>
/// Orchestrates the <c>testlens test</c> command: runs a single <c>dotnet test</c> process,
/// polls TRX output files for live progress, then renders a summary.
/// </summary>
public static class TestHandler
{
    public static async Task<int> ExecuteAsync(string[] passthroughArgs)
    {
        // ── 1. Set up TRX results directory ──────────────────────────────────
        var runDir = Path.Combine(
            Path.GetTempPath(), "testlens",
            $"run-{DateTime.UtcNow:yyyyMMddHHmmss}");
        Directory.CreateDirectory(runDir);

        // ── 2. Launch dotnet test ────────────────────────────────────────────
        AnsiConsole.MarkupLine("[bold]Running tests…[/]");
        AnsiConsole.WriteLine();

        var display = new LiveTestDisplay();

        var testTask = DotnetTestRunner.RunAsync(passthroughArgs, runDir);

        // ── 3. Poll TRX directory for results while dotnet test runs ─────────
        var sw = Stopwatch.StartNew();
        var knownFiles = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        await AnsiConsole.Live(display.Render())
            .AutoClear(true)
            .StartAsync(async ctx =>
            {
                // Single update loop: tick spinner + poll TRX + render.
                // Using one loop avoids concurrent UpdateTarget calls which
                // corrupt Spectre.Console's internal cursor-position tracking.
                const int FrameMs = 120;
                int ticksSincePoll = 0;

                while (!testTask.IsCompleted)
                {
                    await Task.Delay(FrameMs);
                    display.TickSpinner();

                    // Poll TRX directory ~every 250ms (every other frame)
                    ticksSincePoll += FrameMs;
                    if (ticksSincePoll >= 250)
                    {
                        PollTrxDirectory(runDir, knownFiles, display, sw.Elapsed, done: false);
                        ticksSincePoll = 0;
                    }

                    ctx.UpdateTarget(display.Render());
                }

                // Final poll after process exits to catch any last writes
                await Task.Delay(100);
                PollTrxDirectory(runDir, knownFiles, display, sw.Elapsed, done: true);
                ctx.UpdateTarget(display.Render());
            });

        // Render final live state after Live context clears
        // (only if there was actual test data to show)
        if (display.HasProjects)
            AnsiConsole.Write(display.Render());

        var result = await testTask;

        // ── 4. Parse final TRX data for summary ─────────────────────────────
        var trxFiles = Directory.GetFiles(runDir, "*.trx");
        var projectRuns = new List<ProjectRun>();
        foreach (var trx in trxFiles)
        {
            try { projectRuns.Add(TrxParser.Parse(trx)); }
            catch { /* skip unparseable files */ }
        }

        // ── 5. Summary ──────────────────────────────────────────────────────
        SummaryDisplay.Render(projectRuns, result);

        // If dotnet test exited non-zero and we got no TRX data at all,
        // show the captured output (likely a build error).
        if (result.ExitCode != 0 && projectRuns.Count == 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red bold]dotnet test failed with no test results.[/]");
            AnsiConsole.MarkupLine("[dim]Captured output:[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Text(result.Output));
        }

        return result.ExitCode != 0 ? 1 : 0;
    }

    /// <summary>
    /// Scans the results directory for new or updated TRX files and feeds them
    /// into the live display.
    /// </summary>
    private static void PollTrxDirectory(
        string dir,
        Dictionary<string, DateTime> knownFiles,
        LiveTestDisplay display,
        TimeSpan elapsed,
        bool done)
    {
        string[] trxFiles;
        try { trxFiles = Directory.GetFiles(dir, "*.trx"); }
        catch { return; }

        foreach (var path in trxFiles)
        {
            DateTime lastWrite;
            try { lastWrite = File.GetLastWriteTimeUtc(path); }
            catch { continue; }

            // Skip if we've already seen this version
            if (knownFiles.TryGetValue(path, out var prev) && prev == lastWrite && !done)
                continue;

            knownFiles[path] = lastWrite;

            var trx = TrxParser.ParsePartial(path);
            display.AddOrUpdateProject(trx, done, elapsed);
        }
    }
}
