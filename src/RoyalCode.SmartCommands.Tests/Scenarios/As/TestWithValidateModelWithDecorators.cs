using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Tests.Scenarios.As;

public class TestWithValidateModelWithDecorators
{
    [Fact]
    public void CommandThatReturnsAnObject_WithValidateModel_WithDecorators()
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
using System.Diagnostics.CodeAnalysis;

namespace Tests.Scenarios.As;

public class DoSomethingWithValidateModelWithDecorators
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithValidateModelWithDecorators>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators]
    public Some Get() => new Some(Name!);
}

""";

    public const string Interface =
"""
using RoyalCode.SmartCommands.Tests.Scenarios.As;
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoSomethingWithValidateModelWithDecoratorsHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingWithValidateModelWithDecorators command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Tests.Scenarios.As;
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingWithValidateModelWithDecoratorsHandler : IDoSomethingWithValidateModelWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingWithValidateModelWithDecorators, Some>> decorators;

    public DoSomethingWithValidateModelWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingWithValidateModelWithDecorators, Some>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingWithValidateModelWithDecorators command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        var decoratorsMediator = new Mediator<DoSomethingWithValidateModelWithDecorators, Some>(
            this.decorators,
            () => Task.FromResult(command.Get()),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}

""";
}