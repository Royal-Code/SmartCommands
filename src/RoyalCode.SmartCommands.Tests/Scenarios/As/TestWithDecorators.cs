using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Scenarios.As;

public class TestWithDecorators
{
    [Fact]
    public void CommandThatReturnsAnObject_WithDecorators()
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

public class DoSomethingWithDecorators
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Some Get() => new Some(Name ?? throw new Exception("Bad Name"));
}

""";

    public const string Interface =
"""
using RoyalCode.SmartCommands.Tests.Scenarios.As;

namespace Tests.Scenarios.As;

public interface IDoSomethingWithDecoratorsHandler
{
    public Task<Some> HandleAsync(DoSomethingWithDecorators command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Tests.Scenarios.As;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingWithDecoratorsHandler : IDoSomethingWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingWithDecorators, Some>> decorators;

    public DoSomethingWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingWithDecorators, Some>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Some> HandleAsync(DoSomethingWithDecorators command, CancellationToken ct)
    {
        var decoratorsMediator = new Mediator<DoSomethingWithDecorators, Some>(
            this.decorators,
            () => Task.FromResult(command.Get()),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}

""";
}
