using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.As;

public class TestSimple
{
    [Fact]
    public void SimpleCommandThatReturnsAnObject()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(Code.Interface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(Code.Handler);
    }
}

file static class Code
{
    public const string Command =
"""
using Coreum.NewCommands;
using Coreum.NewCommands.Tests.Scenarios.As;

namespace Tests.Scenarios.As;

public class DoSomethingSimple
{
    public string? Name { get; set; }

    [Command]
    public Some Get() => new Some(Name ?? throw new Exception("Bad Name"));
}

""";

    public const string Interface =
"""
using Coreum.NewCommands.Tests.Scenarios.As;

namespace Tests.Scenarios.As;

public interface IDoSomethingSimpleHandler
{
    public Some Handle(DoSomethingSimple command);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands.Tests.Scenarios.As;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingSimpleHandler : IDoSomethingSimpleHandler
{
    public Some Handle(DoSomethingSimple command)
    {
        return command.Get();
    }
}

""";
}