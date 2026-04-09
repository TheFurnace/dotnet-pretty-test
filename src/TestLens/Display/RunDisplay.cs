using Spectre.Console;
using TestLens.Trx;

namespace TestLens.Display;

/// <summary>
/// Renders the live build/test progress and the final summary table for the `run` command.
/// </summary>
public static class RunDisplay
{
    // ── summary table ─────────────────────────────────────────────────────────

    public static void RenderSummary(
        IReadOnlyList<string> allProjects,
        IReadOnlyList<Runner.BuildResult> buildResults,
        IReadOnlyList<(Runner.TestRunResult Run, ProjectRun? Trx)> testResults)
    {
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Project[/]"))
            .AddColumn(new TableColumn("[bold]Total[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Passed[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Failed[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Skipped[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Time[/]").RightAligned());

        int totalAll = 0, passedAll = 0, failedAll = 0, skippedAll = 0;
        var totalTime = TimeSpan.Zero;

        foreach (var project in allProjects)
        {
            var name = Runner.DotnetTestRunner.ProjectName(project);
            var build = buildResults.FirstOrDefault(b => b.ProjectPath == project);

            if (build is not null && build.ExitCode != 0)
            {
                table.AddRow(
                    $"[bold]{Markup.Escape(name)}[/]",
                    "[red]BUILD FAILED[/]",
                    "", "", "",
                    FormatTime(build.Elapsed));
                continue;
            }

            var testEntry = testResults.FirstOrDefault(t => t.Run.ProjectPath == project);
            if (testEntry == default)
            {
                // Project was skipped (no build result = not attempted)
                table.AddRow($"[grey]{Markup.Escape(name)}[/]", "[grey]skipped[/]", "", "", "", "");
                continue;
            }

            var (run, trx) = testEntry;

            if (trx is null)
            {
                // Test ran but TRX missing / unreadable
                var status = run.ExitCode == 0
                    ? "[green]passed[/]"
                    : "[red]FAILED[/]";
                table.AddRow($"[bold]{Markup.Escape(name)}[/]", status, "", "", "", FormatTime(run.Elapsed));
                continue;
            }

            var passedMkp  = trx.Failed > 0 ? trx.Passed.ToString() : $"[green]{trx.Passed}[/]";
            var failedMkp  = trx.Failed > 0 ? $"[red bold]{trx.Failed}[/]" : trx.Failed.ToString();
            var skippedMkp = trx.Skipped > 0 ? $"[yellow]{trx.Skipped}[/]" : trx.Skipped.ToString();
            var nameMkp    = trx.Failed > 0
                ? $"[red bold]{Markup.Escape(name)}[/]"
                : $"[bold]{Markup.Escape(name)}[/]";

            table.AddRow(nameMkp, trx.Total.ToString(), passedMkp, failedMkp, skippedMkp, FormatTime(run.Elapsed));

            totalAll   += trx.Total;
            passedAll  += trx.Passed;
            failedAll  += trx.Failed;
            skippedAll += trx.Skipped;
            totalTime  += run.Elapsed;
        }

        // Totals footer row
        if (testResults.Any(t => t.Trx is not null))
        {
            table.AddEmptyRow();
            var failedTotalMkp  = failedAll  > 0 ? $"[red bold]{failedAll}[/]"  : failedAll.ToString();
            var passedTotalMkp  = failedAll == 0 ? $"[green]{passedAll}[/]"      : passedAll.ToString();
            var skippedTotalMkp = skippedAll > 0 ? $"[yellow]{skippedAll}[/]"   : skippedAll.ToString();

            table.AddRow("[bold]Total[/]", $"[bold]{totalAll}[/]", passedTotalMkp,
                         failedTotalMkp, skippedTotalMkp, FormatTime(totalTime));
        }

        AnsiConsole.Write(table);

        // Overall verdict line
        AnsiConsole.WriteLine();
        if (failedAll > 0)
            AnsiConsole.MarkupLine($"[red bold]✗ {failedAll} test{(failedAll == 1 ? "" : "s")} failed[/]");
        else if (totalAll > 0)
            AnsiConsole.MarkupLine($"[green bold]✓ All {totalAll} tests passed[/]");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    public static string FormatTime(TimeSpan t)
    {
        if (t.TotalSeconds < 1)  return $"{t.TotalMilliseconds:F0}ms";
        if (t.TotalMinutes < 1)  return $"{t.TotalSeconds:F1}s";
        return $"{(int)t.TotalMinutes}m {t.Seconds}s";
    }
}
