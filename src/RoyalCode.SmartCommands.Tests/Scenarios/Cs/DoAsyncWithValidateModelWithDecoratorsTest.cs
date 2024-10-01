using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using RoyalCode.SmartProblems;
using RuleSet = RoyalCode.SmartValidations.RuleSet;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Cs;

public class DoAsyncWithValidateModelWithDecoratorsTest
{
    [Fact]
    public void AsyncCommandThatReturnsAResultWitValueWithValidateModelWithDecorators()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(Code.Interface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(Code.Handler);
    }
}

public class DoSomethingAsyncWithValidateModelWithDecorators
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingAsyncWithValidateModelWithDecorators>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators]
    public async Task<Result<string>> GetAsync() => await Task.FromResult(Name!);
}

public interface IDoSomethingAsyncWithValidateModelWithDecoratorsHandler
{
    public Task<Result<string>> HandleAsync(DoSomethingAsyncWithValidateModelWithDecorators command, CancellationToken ct);
}

public class DoSomethingAsyncWithValidateModelWithDecoratorsHandler : IDoSomethingAsyncWithValidateModelWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingAsyncWithValidateModelWithDecorators, Result<string>>> decorators;

    public DoSomethingAsyncWithValidateModelWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingAsyncWithValidateModelWithDecorators, Result<string>>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result<string>> HandleAsync(DoSomethingAsyncWithValidateModelWithDecorators command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        var decoratorsMediator = new Mediator<DoSomethingAsyncWithValidateModelWithDecorators, Result<string>>(
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

public class DoSomethingAsyncWithValidateModelWithDecorators
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingAsyncWithValidateModelWithDecorators>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators]
    public async Task<Result<string>> GetAsync() => await Task.FromResult(Name!);
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoSomethingAsyncWithValidateModelWithDecoratorsHandler
{
    public Task<Result<string>> HandleAsync(DoSomethingAsyncWithValidateModelWithDecorators command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingAsyncWithValidateModelWithDecoratorsHandler : IDoSomethingAsyncWithValidateModelWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingAsyncWithValidateModelWithDecorators, Result<string>>> decorators;

    public DoSomethingAsyncWithValidateModelWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingAsyncWithValidateModelWithDecorators, Result<string>>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result<string>> HandleAsync(DoSomethingAsyncWithValidateModelWithDecorators command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        var decoratorsMediator = new Mediator<DoSomethingAsyncWithValidateModelWithDecorators, Result<string>>(
            this.decorators,
            async () => await command.GetAsync(),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}

""";
}