using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Bs;

public class TestDoAsyncWithDecorators
{
    [Fact]
    public void AsyncCommandThatReturnsAResult_WithDecorators()
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
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public class DoAsyncWithDecorators
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Task<Result> GetAsync() => Task.FromResult(Result.Ok());
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoAsyncWithDecoratorsHandler
{
    public Task<Result> HandleAsync(DoAsyncWithDecorators command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoAsyncWithDecoratorsHandler : IDoAsyncWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoAsyncWithDecorators, Result>> decorators;

    public DoAsyncWithDecoratorsHandler(IEnumerable<IDecorator<DoAsyncWithDecorators, Result>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result> HandleAsync(DoAsyncWithDecorators command, CancellationToken ct)
    {
        var decoratorsMediator = new Mediator<DoAsyncWithDecorators, Result>(
            this.decorators,
            async () => await command.GetAsync(),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}

""";
}
