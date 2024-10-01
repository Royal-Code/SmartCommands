using FluentAssertions;
using Microsoft.CodeAnalysis;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Cs;

public class DoAsyncWithDecoratorsTest
{
    [Fact]
    public void AsyncCommandThatReturnsAResultWitValueWithDecorators()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(Code.Interface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(Code.Handler);
    }
}

public class DoSomethingAsyncWithDecorators
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public async Task<Result<string>> GetAsync() => await Task.FromResult(Name ?? throw new Exception("Bad name"));
}

public interface IDoSomethingAsyncWithDecoratorsHandler
{
    public Task<Result<string>> HandleAsync(DoSomethingAsyncWithDecorators command, CancellationToken ct);
}

public class DoSomethingAsyncWithDecoratorsHandler : IDoSomethingAsyncWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingAsyncWithDecorators, Result<string>>> decorators;

    public DoSomethingAsyncWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingAsyncWithDecorators, Result<string>>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result<string>> HandleAsync(DoSomethingAsyncWithDecorators command, CancellationToken ct)
    {
        var decoratorsMediator = new Mediator<DoSomethingAsyncWithDecorators, Result<string>>(
            this.decorators,
            async () => await command.GetAsync(),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}

file static class Code
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public class DoSomethingAsyncWithDecorators
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public async Task<Result<string>> GetAsync() => await Task.FromResult(Name ?? throw new Exception("Bad name"));
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoSomethingAsyncWithDecoratorsHandler
{
    public Task<Result<string>> HandleAsync(DoSomethingAsyncWithDecorators command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingAsyncWithDecoratorsHandler : IDoSomethingAsyncWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingAsyncWithDecorators, Result<string>>> decorators;

    public DoSomethingAsyncWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingAsyncWithDecorators, Result<string>>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result<string>> HandleAsync(DoSomethingAsyncWithDecorators command, CancellationToken ct)
    {
        var decoratorsMediator = new Mediator<DoSomethingAsyncWithDecorators, Result<string>>(
            this.decorators,
            async () => await command.GetAsync(),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}

""";
}