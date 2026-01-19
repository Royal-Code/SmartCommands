using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Scenarios.As;

public class TestSimple
{
    [Fact]
    public void SimpleCommandThatReturnsAnObject()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        Assert.Equal(Code.Interface, generatedInterface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        Assert.Equal(Code.Handler, generatedHandler);
    }
}

file static class Code
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Tests.Scenarios.As;

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
using RoyalCode.SmartCommands.Tests.Scenarios.As;

namespace Tests.Scenarios.As;

public interface IDoSomethingSimpleHandler
{
    public Some Handle(DoSomethingSimple command);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands.Tests.Scenarios.As;
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