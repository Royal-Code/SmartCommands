using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Bs;

public class TestDoAsync
{
    [Fact]
    public void AsyncCommandThatReturnsAResult()
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

public class DoAsync
{
    public string? Name { get; set; }

    [Command]
    public Task<Result> GetAsync() => Task.FromResult(Result.Ok());
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoAsyncHandler
{
    public Task<Result> HandleAsync(DoAsync command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoAsyncHandler : IDoAsyncHandler
{
    public async Task<Result> HandleAsync(DoAsync command, CancellationToken ct)
    {
        return await command.GetAsync();
    }
}

""";
}
