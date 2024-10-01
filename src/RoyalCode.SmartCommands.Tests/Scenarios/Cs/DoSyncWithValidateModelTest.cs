using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using RoyalCode.SmartProblems;
using RuleSet = RoyalCode.SmartValidations.RuleSet;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Cs;

public class DoSyncWithValidateModelTest
{
    [Fact]
    public void CommandThatReturnsAResultWitValueWithValidateModel()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(Code.Interface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(Code.Handler);
    }
}

public class DoSomethingSyncWithValidateModel
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingSyncWithValidateModel>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel]
    public Result<string> Get() => Name ?? throw new Exception("Bad name");
}

public interface IDoSomethingSyncWithValidateModelHandler
{
    public Result<string> Handle(DoSomethingSyncWithValidateModel command);
}

public class DoSomethingSyncWithValidateModelHandlerImpl : IDoSomethingSyncWithValidateModelHandler
{
    public Result<string> Handle(DoSomethingSyncWithValidateModel command)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        return command.Get();
    }
}

file static class Code
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public class DoSomethingSyncWithValidateModel
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingSyncWithValidateModel>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel]
    public Result<string> Get() => Name ?? throw new Exception("Bad name");
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoSomethingSyncWithValidateModelHandler
{
    public Result<string> Handle(DoSomethingSyncWithValidateModel command);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoSomethingSyncWithValidateModelHandler : IDoSomethingSyncWithValidateModelHandler
{
    public Result<string> Handle(DoSomethingSyncWithValidateModel command)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        return command.Get();
    }
}

""";
}