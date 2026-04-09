using System.CommandLine;
using TestLens.Commands;

var root = new RootCommand("testlens — pretty test runner and reporter for dotnet test")
{
    ErrorsCommand.Build(),
    RunCommand.Build()    // Phase 2
    // ReportCommand.Build() — Phase 3
};

return await root.InvokeAsync(args);
