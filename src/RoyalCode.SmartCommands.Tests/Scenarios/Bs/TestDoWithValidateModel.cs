using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Bs;

public class TestDoWithValidateModel
{
    [Fact]
    public void CommandThatReturnsAResult_WithValidateModel()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        Assert.Equal(Code.Interface, generatedInterface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        Assert.Equal(Code.Handler, generatedHandler);
    }
}

file static class Code
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public class DoWithValidateModel
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoWithValidateModel>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel]
    public Result Get() => Result.Ok();
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.As;

public interface IDoWithValidateModelHandler
{
    public Result Handle(DoWithValidateModel command);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartProblems;
using Tests.Scenarios.As;

namespace Tests.Scenarios.As.Internals;

public class DoWithValidateModelHandler : IDoWithValidateModelHandler
{
    public Result Handle(DoWithValidateModel command)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        return command.Get();
    }
}

""";
}