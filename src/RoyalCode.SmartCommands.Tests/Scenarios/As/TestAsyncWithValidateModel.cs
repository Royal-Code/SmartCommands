using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Tests.Scenarios.As;

public class TestAsyncWithValidateModel
{
    [Fact]
    public void AsyncCommandThatReturnsAnObject_WithValidateModel()
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
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Tests.Scenarios.As;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.As;

public class DoSomethingAsyncWithValidateModel
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithValidateModel>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel]
    public Task<Some> GetAsync() => Task.FromResult(new Some(Name!));
}
""";
    
    public const string Interface =
"""
using RoyalCode.SmartCommands.Tests.Scenarios.As;
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoSomethingAsyncWithValidateModelHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingAsyncWithValidateModel command, CancellationToken ct);
}

""";
    
    public const string Handler =
"""
using RoyalCode.SmartCommands.Tests.Scenarios.As;
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingAsyncWithValidateModelHandler : IDoSomethingAsyncWithValidateModelHandler
{
    public async Task<Result<Some>> HandleAsync(DoSomethingAsyncWithValidateModel command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        return await command.GetAsync();
    }
}

""";
}