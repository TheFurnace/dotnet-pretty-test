using Spectre.Console;
using Spectre.Console.Rendering;
using TestLens.Trx;

namespace TestLens.Display;

/// <summary>
/// Maintains live per-project test state and renders as a self-updating
/// <see cref="IRenderable"/> that can be passed directly to
/// <c>AnsiConsole.Live()</c>.  The spinner advances automatically on every
/// render call (internal-state auto-mutating renderable pattern); no external
/// tick is required.  All public methods are thread-safe.
/// </summary>
public sealed class LiveTestDisplay : IRenderable
{
    // ── per-project row ───────────────────────────────────────────────────────

    private sealed class RowState
    {
        public required string     TrxPath   { get; init; }
        public required string     Name      { get; set; }
        public bool                Done      { get; set; }
        public ProjectRun?         Trx       { get; set; }
        public int                 PrevCount { get; set; }
        public TimeSpan            Elapsed   { get; set; }
    }

    // ── individual test completion event ──────────────────────────────────────

    private readonly record struct RecentEvent(
        string   Icon,
        string   TestName,
        TimeSpan Duration);

    // ── fields ────────────────────────────────────────────────────────────────

    private readonly List<RowState>     _rows   = [];
    private readonly Queue<RecentEvent> _recent = new();
    private readonly object             _lock   = new();

    private const int MaxRecent = 10;

    private static readonly string[] SpinFrames =
        ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    /// <summary>
    /// Returns the current spinner frame driven by wall-clock time (~8 fps).
    /// Reading time here means no external <c>Tick</c> call is needed — the
    /// spinner advances on its own whenever <see cref="Render"/> is called.
    /// </summary>
    private static string CurrentSpinFrame =>
        SpinFrames[(int)(Environment.TickCount64 / 120) % SpinFrames.Length];

    // ── mutations ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a new project row or updates an existing one.
    /// Projects are identified by TRX file path.
    /// </summary>
    public void AddOrUpdateProject(ProjectRun trx, bool done, TimeSpan elapsed)
    {
        lock (_lock)
        {
            var row = _rows.FirstOrDefault(r =>
                string.Equals(r.TrxPath, trx.TrxPath, StringComparison.OrdinalIgnoreCase));

            if (row is null)
            {
                row = new RowState
                {
                    TrxPath = trx.TrxPath,
                    Name    = trx.ProjectName,
                };
                _rows.Add(row);
            }

            row.Done    = done;
            row.Elapsed = elapsed;
            row.Name    = trx.ProjectName;  // may improve as more data parsed

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

    /// <summary>Whether any project rows have been added.</summary>
    public bool HasProjects
    {
        get { lock (_lock) return _rows.Count > 0; }
    }

    // ── IRenderable ───────────────────────────────────────────────────────────

    /// <summary>
    /// Reports a flexible width so parent containers can size us appropriately.
    /// Min is a reasonable minimum; max fills the available terminal width.
    /// </summary>
    public Measurement Measure(RenderOptions options, int maxWidth)
        => new Measurement(20, maxWidth);

    /// <summary>
    /// Called by the Spectre.Console rendering pipeline (including the live-
    /// display auto-refresh timer) to produce the current frame as segments.
    /// Delegates to <see cref="BuildRenderable"/> so the heavy lifting stays
    /// in one place.
    /// </summary>
    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        => BuildRenderable().Render(options, maxWidth);

    // ── internal renderable builder ───────────────────────────────────────────

    /// <summary>
    /// Builds the current <see cref="IRenderable"/> snapshot under the lock.
    /// </summary>
    private IRenderable BuildRenderable()
    {
        lock (_lock)
        {
            if (_rows.Count == 0 && _recent.Count == 0)
            {
                var spinner = CurrentSpinFrame;
                return new Markup($"  [yellow]{spinner}[/] [dim]Waiting for test results…[/]");
            }

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

            foreach (var ev in _recent)
            {
                var name = ev.TestName.Length > 68
                    ? "…" + ev.TestName[^67..]
                    : ev.TestName;

                parts.Add(new Markup(
                    $"  {ev.Icon}  [dim]{Markup.Escape(name)}[/]  " +
                    $"[grey]{SummaryDisplay.FormatTime(ev.Duration)}[/]"));
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
            icon    = $"[yellow]{CurrentSpinFrame}[/]";
            nameMkp = $"[bold]{Markup.Escape(row.Name)}[/]";

            if (row.Trx is { Total: > 0 } trx)
            {
                passed  = $"[green]{trx.Passed}[/]";
                failed  = trx.Failed  > 0 ? $"[red bold]{trx.Failed}[/]"  : "[dim]0[/]";
                skipped = trx.Skipped > 0 ? $"[yellow]{trx.Skipped}[/]"  : "[dim]0[/]";
                total   = trx.Total.ToString();
                time    = $"[dim]{SummaryDisplay.FormatTime(row.Elapsed)}[/]";
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
            time    = SummaryDisplay.FormatTime(row.Elapsed);
        }
        else
        {
            // Completed but no TRX data.
            icon    = "[red]✗[/]";
            nameMkp = $"[bold]{Markup.Escape(row.Name)}[/]";
            passed = failed = skipped = total = "[dim]?[/]";
            time    = SummaryDisplay.FormatTime(row.Elapsed);
        }

        return [icon, nameMkp, passed, failed, skipped, total, time];
    }
}
