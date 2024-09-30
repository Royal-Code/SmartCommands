using FluentAssertions;
using Microsoft.CodeAnalysis;
using RoyalCode.SmartProblems;

namespace Coreum.NewCommands.Tests.Scenarios.Cs;

public class DoSyncWithDecoratorsTest
{
    [Fact]
    public void CommandThatReturnsAResultWitValueWithDecorators()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(Code.Interface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(Code.Handler);
    }
}

public class DoSomethingSyncWithDecorators
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Result<string> Get() => Name ?? throw new Exception("Bad name");
}

public interface IDoSomethingSyncWithDecoratorsHandler
{
    public Task<Result<string>> HandleAsync(DoSomethingSyncWithDecorators command, CancellationToken ct);
}

public class DoSomethingSyncWithDecoratorsHandler : IDoSomethingSyncWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingSyncWithDecorators, Result<string>>> decorators;

    public DoSomethingSyncWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingSyncWithDecorators, Result<string>>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result<string>> HandleAsync(DoSomethingSyncWithDecorators command, CancellationToken ct)
    {
        var decoratorsMediator = new Mediator<DoSomethingSyncWithDecorators, Result<string>>(
            this.decorators,
            () => Task.FromResult(command.Get()),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}

file static class Code
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public class DoSomethingSyncWithDecorators
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Result<string> Get() => Name ?? throw new Exception("Bad name");
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoSomethingSyncWithDecoratorsHandler
{
    public Task<Result<string>> HandleAsync(DoSomethingSyncWithDecorators command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingSyncWithDecoratorsHandler : IDoSomethingSyncWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingSyncWithDecorators, Result<string>>> decorators;

    public DoSomethingSyncWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingSyncWithDecorators, Result<string>>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result<string>> HandleAsync(DoSomethingSyncWithDecorators command, CancellationToken ct)
    {
        var decoratorsMediator = new Mediator<DoSomethingSyncWithDecorators, Result<string>>(
            this.decorators,
            () => Task.FromResult(command.Get()),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}

""";
}