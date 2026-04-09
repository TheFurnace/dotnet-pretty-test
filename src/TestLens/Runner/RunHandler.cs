using Spectre.Console;
using TestLens.Display;
using TestLens.Runner;
using TestLens.Trx;

namespace TestLens.Runner;

/// <summary>
/// Orchestrates the full build → test → summary flow for the `run` command.
/// </summary>
public static class RunHandler
{
    public static async Task<int> ExecuteAsync(
        string[] paths,
        bool parallel,
        FileInfo[]? failedFrom,
        bool noRestore,
        string configuration)
    {
        // ── 1. Discover projects ──────────────────────────────────────────────
        List<string> projects;
        try
        {
            projects = ProjectDiscovery.Resolve(paths);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }

        if (projects.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No .csproj projects found.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine(
            $"[bold]Found {projects.Count} project{(projects.Count == 1 ? "" : "s")}[/]");

        // ── 2. Build filter map from --failed-from TRX files ─────────────────
        // Maps projectPath → dotnet-test filter expression (or null = run all).
        Dictionary<string, string>? filterMap = null;
        List<string>? projectsToTest = null;

        if (failedFrom is { Length: > 0 })
        {
            (filterMap, projectsToTest) = BuildFailedFromMap(failedFrom, projects);
            if (projectsToTest.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]No failures found in the supplied TRX files — nothing to rerun.[/]");
                return 0;
            }
            AnsiConsole.MarkupLine(
                $"[dim]Rerunning failures from {failedFrom.Length} TRX file(s) across " +
                $"{projectsToTest.Count} project(s).[/]");
        }

        // ── 3. Build phase ────────────────────────────────────────────────────
        var buildResults = await BuildPhaseAsync(projects, configuration, noRestore, parallel);

        var failedBuildPaths = buildResults
            .Where(r => r.ExitCode != 0)
            .Select(r => r.ProjectPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var testCandidates = (projectsToTest ?? projects)
            .Where(p => !failedBuildPaths.Contains(p))
            .ToList();

        if (testCandidates.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]All builds failed — nothing to test.[/]");
            RunDisplay.RenderSummary(projects, buildResults, []);
            return 1;
        }

        // ── 4. Test phase ─────────────────────────────────────────────────────
        var runDir = Path.Combine(Path.GetTempPath(), "testlens", $"run-{DateTime.UtcNow:yyyyMMddHHmmss}");
        Directory.CreateDirectory(runDir);

        var testResults = await TestPhaseAsync(testCandidates, runDir, filterMap, parallel);

        // ── 5. Summary ────────────────────────────────────────────────────────
        RunDisplay.RenderSummary(projects, buildResults, testResults);

        bool anyTestFailed = testResults.Any(t => t.Run.ExitCode != 0);
        return anyTestFailed ? 1 : 0;
    }

    // ── build phase ───────────────────────────────────────────────────────────

    private static async Task<List<BuildResult>> BuildPhaseAsync(
        List<string> projects,
        string configuration,
        bool noRestore,
        bool parallel)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Building…[/]");

        var results = new List<BuildResult>(projects.Count);

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new SpinnerColumn(), new TaskDescriptionColumn())
            .StartAsync(async ctx =>
            {
                var taskMap = projects.ToDictionary(
                    p => p,
                    p => ctx.AddTask(Markup.Escape(DotnetTestRunner.ProjectName(p))));

                async Task BuildOne(string projectPath)
                {
                    var pt = taskMap[projectPath];
                    pt.IsIndeterminate(true);

                    var result = await DotnetTestRunner.BuildAsync(
                        projectPath, configuration, noRestore);

                    lock (results) results.Add(result);

                    pt.Description = result.ExitCode == 0
                        ? $"[green]✓[/] [bold]{Markup.Escape(result.ProjectName)}[/]  " +
                          $"[dim]{RunDisplay.FormatTime(result.Elapsed)}[/]"
                        : $"[red]✗[/] [bold]{Markup.Escape(result.ProjectName)}[/]  " +
                          $"[red]build failed[/]  [dim]{RunDisplay.FormatTime(result.Elapsed)}[/]";
                    pt.StopTask();
                }

                if (parallel)
                    await Task.WhenAll(projects.Select(BuildOne));
                else
                    foreach (var p in projects)
                        await BuildOne(p);
            });

        // Return in original project order.
        return projects
            .Select(p => results.First(r => r.ProjectPath == p))
            .ToList();
    }

    // ── test phase ────────────────────────────────────────────────────────────

    private static async Task<List<(TestRunResult Run, ProjectRun? Trx)>> TestPhaseAsync(
        List<string> projects,
        string runDir,
        Dictionary<string, string>? filterMap,
        bool parallel)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Running tests…[/]");

        var results = new List<(TestRunResult Run, ProjectRun? Trx)>(projects.Count);

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new SpinnerColumn(), new TaskDescriptionColumn())
            .StartAsync(async ctx =>
            {
                var taskMap = projects.ToDictionary(
                    p => p,
                    p => ctx.AddTask(Markup.Escape(DotnetTestRunner.ProjectName(p))));

                async Task TestOne(string projectPath)
                {
                    var pt      = taskMap[projectPath];
                    var name    = DotnetTestRunner.ProjectName(projectPath);
                    var safeIdx = projects.IndexOf(projectPath);
                    var trxDir  = Path.Combine(runDir, $"{safeIdx:D2}-{name}");
                    var trxPath = Path.Combine(trxDir, $"{name}.trx");
                    Directory.CreateDirectory(trxDir);

                    filterMap?.TryGetValue(projectPath, out var filter);

                    pt.IsIndeterminate(true);
                    pt.Description = $"{Markup.Escape(name)}  [dim]running…[/]";

                    // Run the test process and poll the TRX file for live counts.
                    var testTask = DotnetTestRunner.TestAsync(
                        projectPath, trxPath, filterMap?.GetValueOrDefault(projectPath));

                    while (!testTask.IsCompleted)
                    {
                        await Task.Delay(250);
                        if (File.Exists(trxPath))
                        {
                            var partial = TrxParser.ParsePartial(trxPath);
                            if (partial.Total > 0)
                                pt.Description = LiveDescription(name, partial, done: false);
                        }
                    }

                    var run = await testTask;

                    ProjectRun? trx = null;
                    if (File.Exists(trxPath))
                    {
                        trx = TrxParser.ParsePartial(trxPath);
                        pt.Description = LiveDescription(name, trx, done: true);
                    }
                    else
                    {
                        pt.Description = run.ExitCode == 0
                            ? $"[green]✓[/] [bold]{Markup.Escape(name)}[/]  [dim](no TRX)[/]"
                            : $"[red]✗[/] [bold]{Markup.Escape(name)}[/]  [red]test failed[/]";
                    }

                    pt.StopTask();
                    lock (results) results.Add((Run: run, Trx: trx));
                }

                if (parallel)
                    await Task.WhenAll(projects.Select(TestOne));
                else
                    foreach (var p in projects)
                        await TestOne(p);
            });

        // Return in original project order.
        return projects
            .Select(p => results.First(r => r.Run.ProjectPath == p))
            .ToList(); // tuple fields are named Run/Trx
    }

    // ── --failed-from helpers ─────────────────────────────────────────────────

    private static (Dictionary<string, string> FilterMap, List<string> Projects)
        BuildFailedFromMap(FileInfo[] trxFiles, List<string> discoveredProjects)
    {
        // Parse all TRX files, group failures by project name.
        var failuresByProject = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in trxFiles)
        {
            if (!file.Exists)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] TRX file not found: {Markup.Escape(file.FullName)}");
                continue;
            }

            ProjectRun run;
            try   { run = TrxParser.Parse(file.FullName); }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]Warning:[/] Could not parse {Markup.Escape(file.Name)}: {Markup.Escape(ex.Message)}");
                continue;
            }

            foreach (var failure in run.Failures)
            {
                if (!failuresByProject.TryGetValue(run.ProjectName, out var list))
                    failuresByProject[run.ProjectName] = list = [];
                list.Add(failure.FullyQualifiedName);
            }
        }

        // Match TRX project names to discovered .csproj files by project name.
        var filterMap  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var toTest     = new List<string>();

        foreach (var project in discoveredProjects)
        {
            var name = DotnetTestRunner.ProjectName(project);
            if (!failuresByProject.TryGetValue(name, out var fqns)) continue;

            var filter = string.Join("|",
                fqns.Distinct(StringComparer.Ordinal)
                    .Select(f => $"FullyQualifiedName={EscapeFilterValue(f)}"));

            filterMap[project] = filter;
            toTest.Add(project);
        }

        return (filterMap, toTest);
    }

    // dotnet test filter values: escape backslash and parentheses.
    private static string EscapeFilterValue(string value) =>
        value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    // ── rendering ─────────────────────────────────────────────────────────────

    private static string LiveDescription(string name, ProjectRun run, bool done)
    {
        var icon = done
            ? (run.Failed > 0 ? "[red]✗[/]" : "[green]✓[/]")
            : "[yellow]▶[/]";

        var counts = run.Total == 0
            ? "[dim]0 tests[/]"
            : $"[green]{run.Passed}[/] passed" +
              (run.Failed  > 0 ? $"  [red bold]{run.Failed} failed[/]"  : "") +
              (run.Skipped > 0 ? $"  [yellow]{run.Skipped} skipped[/]" : "");

        return $"{icon} [bold]{Markup.Escape(name)}[/]  {counts}";
    }
}
