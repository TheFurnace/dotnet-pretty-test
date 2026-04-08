using Spectre.Console;
using TestLens.Trx;

namespace TestLens.Display;

public static class ErrorDisplay
{
    public static void Render(IReadOnlyList<ProjectRun> runs, string? nameFilter, bool showOutput)
    {
        var failures = runs
            .SelectMany(r => r.Failures.Select(f => (Run: r, Test: f)))
            .Where(x => nameFilter is null || x.Test.TestName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (failures.Count == 0)
        {
            AnsiConsole.MarkupLine(nameFilter is null
                ? "[green]No failures found.[/]"
                : $"[green]No failures matching '[bold]{Markup.Escape(nameFilter)}[/]'.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[red bold]✗ {failures.Count} failed test{(failures.Count == 1 ? "" : "s")}[/]");
        AnsiConsole.WriteLine();

        string? currentProject = null;

        foreach (var (run, test) in failures)
        {
            if (run.ProjectName != currentProject)
            {
                currentProject = run.ProjectName;
                AnsiConsole.MarkupLine($"[grey]── [bold]{Markup.Escape(currentProject)}[/] ──────────────────────────────────[/]");
                AnsiConsole.WriteLine();
            }

            AnsiConsole.MarkupLine($"  [red bold]FAIL[/]  [bold]{Markup.Escape(test.TestName)}[/]");

            if (test.ErrorMessage is not null)
            {
                foreach (var line in test.ErrorMessage.Split('\n'))
                    AnsiConsole.MarkupLine($"        [yellow]{Markup.Escape(line.TrimEnd())}[/]");
            }

            if (test.StackTrace is not null)
            {
                AnsiConsole.WriteLine();
                foreach (var line in test.StackTrace.Split('\n'))
                    AnsiConsole.MarkupLine($"        [grey]{Markup.Escape(line.TrimEnd())}[/]");
            }

            if (showOutput)
            {
                if (test.StdOut is not null)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("        [dim]── stdout ──[/]");
                    foreach (var line in test.StdOut.Split('\n'))
                        AnsiConsole.MarkupLine($"        [dim]{Markup.Escape(line.TrimEnd())}[/]");
                }
                if (test.StdErr is not null)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("        [dim]── stderr ──[/]");
                    foreach (var line in test.StdErr.Split('\n'))
                        AnsiConsole.MarkupLine($"        [dim]{Markup.Escape(line.TrimEnd())}[/]");
                }
            }

            AnsiConsole.WriteLine();
        }
    }
}
