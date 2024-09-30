using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Coreum.NewCommands.Tests.Scenarios.As;

public class DoSomethingAsyncWithValidateModel
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
    public Task<Some> GetAsync() => Task.FromResult(new Some(Name!));
}

public interface IDoSomethingAsyncWithValidateModelHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingAsyncWithValidateModel command, CancellationToken ct);
}

public class DoSomethingAsyncWithValidateModelHandler : IDoSomethingAsyncWithValidateModelHandler
{
    public async Task<Result<Some>> HandleAsync(DoSomethingAsyncWithValidateModel command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        return await command.GetAsync();
    }
}