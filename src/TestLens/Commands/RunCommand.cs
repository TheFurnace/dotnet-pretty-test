using System.CommandLine;
using Spectre.Console;
using TestLens.Runner;

namespace TestLens.Commands;

public static class RunCommand
{
    public static Command Build()
    {
        var pathsArg = new Argument<string[]>("paths")
        {
            Description = "Projects (.csproj), solutions (.sln), or directories to test. " +
                          "Defaults to the current directory.",
            Arity = ArgumentArity.ZeroOrMore,
        };

        var parallelOpt = new Option<bool>("--parallel")
        {
            Description = "Build and test multiple projects concurrently.",
        };

        var failedFromOpt = new Option<FileInfo[]?>("--failed-from")
        {
            Description = "Re-run only the failing tests from one or more previous TRX result files.",
            Arity = ArgumentArity.OneOrMore,
        };
        failedFromOpt.AllowMultipleArgumentsPerToken = true;

        var noRestoreOpt = new Option<bool>("--no-restore")
        {
            Description = "Skip dotnet restore during the build phase.",
        };

        var configOpt = new Option<string>(["--configuration", "-c"])
        {
            Description = "Build configuration to use (e.g. Debug, Release). Defaults to Debug.",
        };
        configOpt.SetDefaultValue("Debug");

        var cmd = new Command("run", "Build projects and run their tests, showing live progress and a summary table.")
        {
            pathsArg,
            parallelOpt,
            failedFromOpt,
            noRestoreOpt,
            configOpt,
        };

        cmd.SetHandler(async (paths, parallel, failedFrom, noRestore, configuration) =>
        {
            var exitCode = await RunHandler.ExecuteAsync(paths, parallel, failedFrom, noRestore, configuration);
            Environment.Exit(exitCode);
        }, pathsArg, parallelOpt, failedFromOpt, noRestoreOpt, configOpt);

        return cmd;
    }
}
