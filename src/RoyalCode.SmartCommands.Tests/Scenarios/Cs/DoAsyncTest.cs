using FluentAssertions;
using Microsoft.CodeAnalysis;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Cs;

public class DoAsyncTest
{
    [Fact]
    public void AsyncCommandThatReturnsAResultWitValue()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(Code.Interface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(Code.Handler);
    }
}

public class DoSomethingAsync
{
    public string? Name { get; set; }

    [Command]
    public async Task<Result<string>> GetAsync()
    {
        await Task.Delay(1);
        return Name ?? throw new Exception("Bad name");
    }
}

public interface IDoSomethingAsyncHandler
{
    public Task<Result<string>> HandleAsync(DoSomethingAsync command, CancellationToken ct);
}

public class DoSomethingAsyncHandler : IDoSomethingAsyncHandler
{
    public async Task<Result<string>> HandleAsync(DoSomethingAsync command, CancellationToken ct)
    {
        return await command.GetAsync();
    }
}

file static class Code
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public class DoSomethingAsync
{
    public string? Name { get; set; }

    [Command]
    public async Task<Result<string>> GetAsync()
    {
        await Task.Delay(1);
        return Name ?? throw new Exception("Bad name");
    }
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoSomethingAsyncHandler
{
    public Task<Result<string>> HandleAsync(DoSomethingAsync command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingAsyncHandler : IDoSomethingAsyncHandler
{
    public async Task<Result<string>> HandleAsync(DoSomethingAsync command, CancellationToken ct)
    {
        return await command.GetAsync();
    }
}

""";
}