using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.Es;

[MapPost("", "some-create")]
public partial class CreateSomeSync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeSync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Some Create()
    {
        //WasValidated(); // só existe no teste
        return new Some { Id = 1, Name = Name };
    }
}

public interface ICreateSomeSyncHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeSync command, CancellationToken ct);
}

public class CreateSomeSyncHandler : ICreateSomeSyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public CreateSomeSyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeSync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var commandResult = command.Create();

        await this.accessor.AddEntityAsync(commandResult, ct);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

public static class CreateSomeSyncCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Es;

public partial class CreateSomeSync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeSync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Some Create()
    {
        WasValidated();
        return new Some { Id = 1, Name = Name };
    }
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Es;

public interface ICreateSomeSyncHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeSync command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Es;

namespace Tests.Scenarios.Es.Internals;

public class CreateSomeSyncHandler : ICreateSomeSyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public CreateSomeSyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeSync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var commandResult = command.Create();

        await this.accessor.AddEntityAsync(commandResult, ct);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

""";
}