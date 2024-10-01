using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Bs;

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

public interface IDoWithValidateModelHandler
{
    public Result Handle(DoWithValidateModel command);
}

public class DoWithValidateModelHandler : IDoWithValidateModelHandler
{
    public Result Handle(DoWithValidateModel command)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        return command.Get();
    }
}