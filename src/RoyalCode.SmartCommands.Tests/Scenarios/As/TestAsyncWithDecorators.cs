using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.As;

public class TestAsyncWithDecorators
{
    [Fact]
    public void AsyncCommandThatReturnsAnObject_WithDecorators()
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

public class DoSomethingAsyncWithDecorators
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Task<Some> GetAsync() => Task.FromResult(new Some(Name ?? throw new Exception("Bad Name")));
}
""";
    
    public const string Interface =
"""
using Coreum.NewCommands.Tests.Scenarios.As;

namespace Tests.Scenarios.As;

public interface IDoSomethingAsyncWithDecoratorsHandler
{
    public Task<Some> HandleAsync(DoSomethingAsyncWithDecorators command, CancellationToken ct);
}

""";
    
    public const string Handler =
"""
using Coreum.NewCommands;
using Coreum.NewCommands.Tests.Scenarios.As;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingAsyncWithDecoratorsHandler : IDoSomethingAsyncWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingAsyncWithDecorators, Some>> decorators;

    public DoSomethingAsyncWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingAsyncWithDecorators, Some>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Some> HandleAsync(DoSomethingAsyncWithDecorators command, CancellationToken ct)
    {
        var decoratorsMediator = new Mediator<DoSomethingAsyncWithDecorators, Some>(
            this.decorators,
            async () => await command.GetAsync(),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}

""";
}