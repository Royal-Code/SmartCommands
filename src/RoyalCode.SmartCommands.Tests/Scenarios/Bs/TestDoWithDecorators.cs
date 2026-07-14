using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Bs;

public class TestDoWithDecorators
{
    [Fact]
    public void CommandThatReturnsAResult_WithDecorators()
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

public class DoWithDecorators
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Result Get() => Result.Ok();
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoWithDecoratorsHandler
{
    public Task<Result> HandleAsync(DoWithDecorators command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoWithDecoratorsHandler : IDoWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoWithDecorators, Result>> decorators;

    public DoWithDecoratorsHandler(IEnumerable<IDecorator<DoWithDecorators, Result>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result> HandleAsync(DoWithDecorators command, CancellationToken ct)
    {
        var decoratorsMediator = new Mediator<DoWithDecorators, Result>(
            this.decorators,
            () => Task.FromResult(command.Get()),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}

""";
}
