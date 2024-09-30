using FluentAssertions;
using Microsoft.CodeAnalysis;
using RoyalCode.SmartProblems;

namespace Coreum.NewCommands.Tests.Scenarios.Cs;

public class DoSyncTest
{
    [Fact]
    public void CommandThatReturnsAResultWitValue()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(Code.Interface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(Code.Handler);
    }
}

public class DoSomethingSync
{
    public string? Name { get; set; }

    [Command]
    public Result<string> Get() => Name ?? throw new Exception("Bad Name");
}

public interface IDoSomethingSyncHandler
{
    public Result<string> Handle(DoSomethingSync command);
}

public class DoSomethingSyncHandler : IDoSomethingSyncHandler
{
    public Result<string> Handle(DoSomethingSync command)
    {
        return command.Get();
    }
}

file static class Code
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public class DoSomethingSync
{
    public string? Name { get; set; }

    [Command]
    public Result<string> Get() => Name ?? throw new Exception("Bad Name");
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoSomethingSyncHandler
{
    public Result<string> Handle(DoSomethingSync command);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingSyncHandler : IDoSomethingSyncHandler
{
    public Result<string> Handle(DoSomethingSync command)
    {
        return command.Get();
    }
}

""";
}