using System.CommandLine;
using Spectre.Console;
using TestLens.Display;
using TestLens.Trx;

namespace TestLens.Commands;

public static class ErrorsCommand
{
    public static Command Build()
    {
        var filesArg = new Argument<FileInfo[]>("files")
        {
            Description  = "One or more TRX result files to inspect.",
            Arity        = ArgumentArity.OneOrMore,
        };

        var filterOpt = new Option<string?>("--filter", "-f")
        {
            Description = "Show only failures whose test name contains this string (case-insensitive).",
        };

        var showOutputOpt = new Option<bool>("--show-output")
        {
            Description = "Also print captured stdout/stderr from each failing test.",
        };

        var cmd = new Command("errors", "Show detailed failure information from TRX result files.")
        {
            filesArg,
            filterOpt,
            showOutputOpt,
        };

        cmd.SetHandler((files, filter, showOutput) =>
        {
            var runs = new List<ProjectRun>();

            foreach (var file in files)
            {
                if (!file.Exists)
                {
                    AnsiConsole.MarkupLine($"[red]File not found:[/] {Markup.Escape(file.FullName)}");
                    return;
                }

                try
                {
                    runs.Add(TrxParser.Parse(file.FullName));
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to parse[/] {Markup.Escape(file.Name)}: {Markup.Escape(ex.Message)}");
                    return;
                }
            }

            ErrorDisplay.Render(runs, filter, showOutput);

        }, filesArg, filterOpt, showOutputOpt);

        return cmd;
    }
}
