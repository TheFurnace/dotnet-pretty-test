using Spectre.Console;
using Spectre.Console.Rendering;
using TestLens.Runner;
using TestLens.Trx;

namespace TestLens.Display;

/// <summary>
/// Maintains live per-project test state and renders it as a Spectre.Console renderable.
/// Intended for use with <c>AnsiConsole.Live()</c> during the test phase.
/// All public methods are thread-safe.
/// </summary>
public sealed class LiveTestDisplay
{
    // ── per-project row ───────────────────────────────────────────────────────

    private sealed class RowState
    {
        public required string     ProjectPath { get; init; }
        public required string     Name        { get; init; }
        public bool                Done        { get; set; }
        public int                 ExitCode    { get; set; }
        public ProjectRun?         Trx         { get; set; }
        public int                 PrevCount   { get; set; }
        public TimeSpan            Elapsed     { get; set; }
    }

    // ── individual test completion event ──────────────────────────────────────

    private readonly record struct RecentEvent(
        string   Icon,
        string   TestName,
        TimeSpan Duration);

    // ── fields ────────────────────────────────────────────────────────────────

    private readonly List<RowState>     _rows;
    private readonly Queue<RecentEvent> _recent = new();
    private int                         _spinFrame;
    private readonly object             _lock   = new();

    private const int MaxRecent = 10;

    private static readonly string[] SpinFrames =
        ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    // ── construction ──────────────────────────────────────────────────────────

    public LiveTestDisplay(IEnumerable<string> projectPaths)
    {
        _rows = projectPaths
            .Select(p => new RowState
            {
                ProjectPath = p,
                Name        = DotnetTestRunner.ProjectName(p),
            })
            .ToList();
    }

    // ── mutations ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by each test task whenever new TRX data is available or the task completes.
    /// </summary>
    public void UpdateProject(
        string      projectPath,
        ProjectRun? trx,
        bool        done,
        int         exitCode,
        TimeSpan    elapsed)
    {
        lock (_lock)
        {
            var row = _rows.First(r => r.ProjectPath == projectPath);
            row.Done     = done;
            row.ExitCode = exitCode;
            row.Elapsed  = elapsed;

            if (trx is not null)
            {
                // Enqueue any test results that appeared since the last poll.
                foreach (var result in trx.Results.Skip(row.PrevCount))
                {
                    var icon = result.Outcome switch
                    {
                        TestOutcome.Passed => "[green]✓[/]",
                        TestOutcome.Failed => "[red]✗[/]",
                        _                 => "[grey]-[/]",
                    };
                    _recent.Enqueue(new RecentEvent(icon, result.TestName, result.Duration));
                    while (_recent.Count > MaxRecent)
                        _recent.Dequeue();
                }
                row.PrevCount = trx.Results.Count;
                row.Trx       = trx;
            }
        }
    }

    /// <summary>Advances the spinner animation by one frame.</summary>
    public void TickSpinner()
    {
        lock (_lock)
            _spinFrame = (_spinFrame + 1) % SpinFrames.Length;
    }

    // ── rendering ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the current <see cref="IRenderable"/> from a snapshot of state.
    /// Safe to call from any thread.
    /// </summary>
    public IRenderable Render()
    {
        lock (_lock)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("").Width(3).NoWrap())
                .AddColumn(new TableColumn("[bold]Project[/]"))
                .AddColumn(new TableColumn("[bold]Passed[/]").RightAligned().Width(8))
                .AddColumn(new TableColumn("[bold]Failed[/]").RightAligned().Width(8))
                .AddColumn(new TableColumn("[bold]Skipped[/]").RightAligned().Width(9))
                .AddColumn(new TableColumn("[bold]Total[/]").RightAligned().Width(7))
                .AddColumn(new TableColumn("[bold]Time[/]").RightAligned().Width(7));

            foreach (var row in _rows)
                table.AddRow(BuildRowCells(row));

            if (_recent.Count == 0)
                return table;

            // Append recent individual test completions below the table.
            var parts = new List<IRenderable>
            {
                table,
                new Text(""),
                new Markup(" [dim]Recent tests:[/]"),
            };

            foreach (var ev in _recent)   // oldest → newest (log order, newest at bottom)
            {
                var name = ev.TestName.Length > 68
                    ? "…" + ev.TestName[^67..]
                    : ev.TestName;

                parts.Add(new Markup(
                    $"  {ev.Icon}  [dim]{Markup.Escape(name)}[/]  " +
                    $"[grey]{RunDisplay.FormatTime(ev.Duration)}[/]"));
            }

            return new Rows(parts);
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private string[] BuildRowCells(RowState row)
    {
        string icon, nameMkp, passed, failed, skipped, total, time;

        if (!row.Done)
        {
            // Still running — show spinner and whatever partial counts we have.
            icon    = $"[yellow]{SpinFrames[_spinFrame]}[/]";
            nameMkp = $"[bold]{Markup.Escape(row.Name)}[/]";

            if (row.Trx is { Total: > 0 } trx)
            {
                passed  = $"[green]{trx.Passed}[/]";
                failed  = trx.Failed  > 0 ? $"[red bold]{trx.Failed}[/]"  : "[dim]0[/]";
                skipped = trx.Skipped > 0 ? $"[yellow]{trx.Skipped}[/]"  : "[dim]0[/]";
                total   = trx.Total.ToString();
                time    = $"[dim]{RunDisplay.FormatTime(row.Elapsed)}[/]";
            }
            else
            {
                passed = failed = skipped = total = "[dim]…[/]";
                time   = "";
            }
        }
        else if (row.Trx is { } trx)
        {
            // Completed with TRX data.
            bool hasFail = trx.Failed > 0;
            icon    = hasFail ? "[red]✗[/]" : "[green]✓[/]";
            nameMkp = hasFail
                ? $"[red bold]{Markup.Escape(row.Name)}[/]"
                : $"[bold]{Markup.Escape(row.Name)}[/]";
            passed  = hasFail ? trx.Passed.ToString()           : $"[green]{trx.Passed}[/]";
            failed  = hasFail ? $"[red bold]{trx.Failed}[/]"   : "0";
            skipped = trx.Skipped > 0 ? $"[yellow]{trx.Skipped}[/]" : "0";
            total   = trx.Total.ToString();
            time    = RunDisplay.FormatTime(row.Elapsed);
        }
        else
        {
            // Completed but no TRX written (test process crashed, etc.).
            icon    = row.ExitCode == 0 ? "[green]✓[/]" : "[red]✗[/]";
            nameMkp = $"[bold]{Markup.Escape(row.Name)}[/]";
            passed = failed = skipped = total = "[dim]?[/]";
            time    = RunDisplay.FormatTime(row.Elapsed);
        }

        return [icon, nameMkp, passed, failed, skipped, total, time];
    }
}
