using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.Es;

public partial class CreateSomeAsync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeAsync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Task<Some> CreateAsync()
    {
        //WasValidated(); // só existe no teste
        var some = new Some { Id = 1, Name = Name };
        return Task.FromResult(some);
    }
}

public interface ICreateSomeAsyncHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeAsync command, CancellationToken ct);
}

public class CreateSomeAsyncHandler : ICreateSomeAsyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public CreateSomeAsyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeAsync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var commandResult = await command.CreateAsync();

        await this.accessor.AddEntityAsync(commandResult, ct);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

public static class CreateSomeAsyncCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Es;

public partial class CreateSomeAsync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeAsync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Task<Some> CreateAsync()
    {
        WasValidated();
        var some = new Some { Id = 1, Name = Name };
        return Task.FromResult(some);
    }
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Es;

public interface ICreateSomeAsyncHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeAsync command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Es;

namespace Tests.Scenarios.Es.Internals;

public class CreateSomeAsyncHandler : ICreateSomeAsyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public CreateSomeAsyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeAsync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var commandResult = await command.CreateAsync();

        await this.accessor.AddEntityAsync(commandResult, ct);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

""";
}