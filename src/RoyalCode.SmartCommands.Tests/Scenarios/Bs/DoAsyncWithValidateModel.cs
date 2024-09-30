using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.Bs;

public class DoAsyncWithValidateModel
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoAsyncWithValidateModel>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel]
    public Task<Result> GetAsync() => Task.FromResult(Result.Ok());
}

public interface IDoAsyncWithValidateModelHandler
{
    public Task<Result> HandleAsync(DoAsyncWithValidateModel command, CancellationToken ct);
}

public class DoAsyncWithValidateModelHandler : IDoAsyncWithValidateModelHandler
{
    public async Task<Result> HandleAsync(DoAsyncWithValidateModel command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        return await command.GetAsync();
    }
}