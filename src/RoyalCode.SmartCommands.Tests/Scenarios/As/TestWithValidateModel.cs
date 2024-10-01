using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Tests.Scenarios.As;

public class TestWithValidateModel
{
    [Fact]
    public void CommandThatReturnsAnObject_WithValidateModel()
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

public class DoSomethingWithValidateModel
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
    public Some Get() => new Some(Name!);
}

""";

    public const string Interface =
"""
using RoyalCode.SmartCommands.Tests.Scenarios.As;
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoSomethingWithValidateModelHandler
{
    public Result<Some> Handle(DoSomethingWithValidateModel command);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands.Tests.Scenarios.As;
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingWithValidateModelHandler : IDoSomethingWithValidateModelHandler
{
    public Result<Some> Handle(DoSomethingWithValidateModel command)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        return command.Get();
    }
}

""";
}