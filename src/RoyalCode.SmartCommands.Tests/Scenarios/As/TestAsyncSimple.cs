using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Scenarios.As;

public class TestAsyncSimple
{
    [Fact]
    public void SimpleAsyncCommandThatReturnsAnObject()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        Assert.Equal(Util.GeneratedCode(Code.Interface), generatedInterface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        Assert.Equal(Util.GeneratedCode(Code.Handler), generatedHandler);
    }
}

file static class Code
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Tests.Scenarios.As;

namespace Tests.Scenarios.As;

public class DoSomethingAsyncSimple
{
    public string? Name { get; set; }

    [Command]
    public Task<Some> GetAsync() => Task.FromResult(new Some(Name ?? throw new Exception("Bad Name")));
}

""";
    
    public const string Interface =
"""
using RoyalCode.SmartCommands.Tests.Scenarios.As;

namespace Tests.Scenarios.As;

public interface IDoSomethingAsyncSimpleHandler
{
    public Task<Some> HandleAsync(DoSomethingAsyncSimple command, CancellationToken ct);
}

""";
    
    public const string Handler =
"""
using RoyalCode.SmartCommands.Tests.Scenarios.As;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingAsyncSimpleHandler : IDoSomethingAsyncSimpleHandler
{
    public async Task<Some> HandleAsync(DoSomethingAsyncSimple command, CancellationToken ct)
    {
        return await command.GetAsync();
    }
}

""";
}
