using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using RoyalCode.SmartProblems;
using RuleSet = RoyalCode.SmartValidations.RuleSet;

namespace Coreum.NewCommands.Tests.Scenarios.Cs;

public class DoSyncWithValidateModelWithDecoratorsTest
{
    [Fact]
    public void CommandThatReturnsAResultWitValueWithValidateModelWithDecorators()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(Code.Interface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(Code.Handler);
    }
}

public class DoSomethingSyncWithValidateModelWithDecorators
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingSyncWithValidateModelWithDecorators>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators]
    public Result<string> Get() => Name!;
}

public interface IDoSomethingSyncWithValidateModelWithDecoratorsHandler
{
    public Task<Result<string>> HandleAsync(DoSomethingSyncWithValidateModelWithDecorators command, CancellationToken ct);
}

public class DoSomethingSyncWithValidateModelWithDecoratorsHandler : IDoSomethingSyncWithValidateModelWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingSyncWithValidateModelWithDecorators, Result<string>>> decorators;

    public DoSomethingSyncWithValidateModelWithDecoratorsHandler(
        IEnumerable<IDecorator<DoSomethingSyncWithValidateModelWithDecorators, Result<string>>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result<string>> HandleAsync(DoSomethingSyncWithValidateModelWithDecorators command,
        CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        var decoratorsMediator = new Mediator<DoSomethingSyncWithValidateModelWithDecorators, Result<string>>(
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

public class DoSomethingSyncWithValidateModelWithDecorators
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingSyncWithValidateModelWithDecorators>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators]
    public Result<string> Get() => Name!;
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoSomethingSyncWithValidateModelWithDecoratorsHandler
{
    public Task<Result<string>> HandleAsync(DoSomethingSyncWithValidateModelWithDecorators command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingSyncWithValidateModelWithDecoratorsHandler : IDoSomethingSyncWithValidateModelWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingSyncWithValidateModelWithDecorators, Result<string>>> decorators;

    public DoSomethingSyncWithValidateModelWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingSyncWithValidateModelWithDecorators, Result<string>>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result<string>> HandleAsync(DoSomethingSyncWithValidateModelWithDecorators command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        var decoratorsMediator = new Mediator<DoSomethingSyncWithValidateModelWithDecorators, Result<string>>(
            this.decorators,
            () => Task.FromResult(command.Get()),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}

""";
}