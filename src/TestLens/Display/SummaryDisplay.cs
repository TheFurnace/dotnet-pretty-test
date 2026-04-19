using Spectre.Console;
using TestLens.Runner;
using TestLens.Trx;

namespace TestLens.Display;

/// <summary>
/// Renders the final summary table after all tests have completed.
/// </summary>
public static class SummaryDisplay
{
    public static void Render(
        IReadOnlyList<ProjectRun> projectRuns,
        TestRunResult runResult)
    {
        AnsiConsole.WriteLine();

        if (projectRuns.Count == 0)
            return;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Project[/]"))
            .AddColumn(new TableColumn("[bold]Total[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Passed[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Failed[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Skipped[/]").RightAligned());

        int totalAll = 0, passedAll = 0, failedAll = 0, skippedAll = 0;

        foreach (var trx in projectRuns)
        {
            var name = trx.ProjectName;

            var passedMkp  = trx.Failed > 0 ? trx.Passed.ToString() : $"[green]{trx.Passed}[/]";
            var failedMkp  = trx.Failed > 0 ? $"[red bold]{trx.Failed}[/]" : trx.Failed.ToString();
            var skippedMkp = trx.Skipped > 0 ? $"[yellow]{trx.Skipped}[/]" : trx.Skipped.ToString();
            var nameMkp    = trx.Failed > 0
                ? $"[red bold]{Markup.Escape(name)}[/]"
                : $"[bold]{Markup.Escape(name)}[/]";

            table.AddRow(nameMkp, trx.Total.ToString(), passedMkp, failedMkp, skippedMkp);

            totalAll   += trx.Total;
            passedAll  += trx.Passed;
            failedAll  += trx.Failed;
            skippedAll += trx.Skipped;
        }

        // Totals footer row
        if (projectRuns.Count > 1)
        {
            table.AddEmptyRow();
            var failedTotalMkp  = failedAll  > 0 ? $"[red bold]{failedAll}[/]"  : failedAll.ToString();
            var passedTotalMkp  = failedAll == 0 ? $"[green]{passedAll}[/]"      : passedAll.ToString();
            var skippedTotalMkp = skippedAll > 0 ? $"[yellow]{skippedAll}[/]"   : skippedAll.ToString();

            table.AddRow("[bold]Total[/]", $"[bold]{totalAll}[/]", passedTotalMkp,
                         failedTotalMkp, skippedTotalMkp);
        }

        AnsiConsole.Write(table);

        // Time line
        AnsiConsole.MarkupLine($"[dim]  Total time: {FormatTime(runResult.Elapsed)}[/]");

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
