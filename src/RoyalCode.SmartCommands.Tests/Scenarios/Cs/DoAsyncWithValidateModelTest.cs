using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using RoyalCode.SmartProblems;
using RuleSet = RoyalCode.SmartValidations.RuleSet;

namespace Coreum.NewCommands.Tests.Scenarios.Cs;

public class DoAsyncWithValidateModelTest
{
    [Fact]
    public void AsyncCommandThatReturnsAResultWitValueWithValidateModel()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(Code.Interface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(Code.Handler);
    }
}

public class DoSomethingAsyncWithValidateModel
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingAsyncWithValidateModel>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel]
    public async Task<Result<string>> GetAsync()
    {
        await Task.Delay(1);
        return Name ?? throw new Exception("Bad name");
    }
}

public interface IDoSomethingAsyncWithValidateModelHandler
{
    public Task<Result<string>> HandleAsync(DoSomethingAsyncWithValidateModel command, CancellationToken ct);
}

public class DoSomethingAsyncWithValidateModelHandler : IDoSomethingAsyncWithValidateModelHandler
{
    public async Task<Result<string>> HandleAsync(DoSomethingAsyncWithValidateModel command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        return await command.GetAsync();
    }
}

file static class Code
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public class DoSomethingAsyncWithValidateModel
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingAsyncWithValidateModel>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel]
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

public interface IDoSomethingAsyncWithValidateModelHandler
{
    public Task<Result<string>> HandleAsync(DoSomethingAsyncWithValidateModel command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingAsyncWithValidateModelHandler : IDoSomethingAsyncWithValidateModelHandler
{
    public async Task<Result<string>> HandleAsync(DoSomethingAsyncWithValidateModel command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        return await command.GetAsync();
    }
}

""";
}