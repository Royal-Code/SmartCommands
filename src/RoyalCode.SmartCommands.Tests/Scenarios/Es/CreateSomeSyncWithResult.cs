using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Es;

public class CreateSomeSyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeSyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Result<Some> Create()
    {
        // WasValidated(); // só funciona nos testes
        return new Some { Id = 1, Name = Name };
    }
}

public interface ICreateSomeSyncWithResultHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeSyncWithResult command, CancellationToken ct);
}

public class CreateSomeSyncWithResultHandler : ICreateSomeSyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public CreateSomeSyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeSyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.Create()
            .ContinueAsync(this.accessor, async (e, a) => await a.AddEntityAsync(e, ct))
            .ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

public static class CreateSomeSyncWithResultCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Es;

public class CreateSomeSyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeSyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Result<Some> Create()
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

public interface ICreateSomeSyncWithResultHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeSyncWithResult command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Es;

namespace Tests.Scenarios.Es.Internals;

public class CreateSomeSyncWithResultHandler : ICreateSomeSyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public CreateSomeSyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeSyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.Create()
            .ContinueAsync(this.accessor, async (e, a) => await a.AddEntityAsync(e, ct))
            .ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

""";
}