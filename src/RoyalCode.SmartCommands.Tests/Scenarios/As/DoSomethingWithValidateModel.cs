using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.As;

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

public interface IDoSomethingWithValidateModelHandler
{
    public Result<Some> Handle(DoSomethingWithValidateModel command);
}

public class DoSomethingWithValidateModelHandler : IDoSomethingWithValidateModelHandler
{
    public Result<Some> Handle(DoSomethingWithValidateModel command)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        return command.Get();
    }
}