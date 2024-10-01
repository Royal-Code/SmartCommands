using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Es;

public partial class CreateSomeAsyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeAsyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Task<Result<Some>> CreateAsync()
    {
        // WasValidated(); // somente em testes
        Result<Some> result = new Some { Id = 1, Name = Name };
        return Task.FromResult(result);
    }
}

public interface ICreateSomeAsyncWithResultHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeAsyncWithResult command, CancellationToken ct);
}

public class CreateSomeAsyncWithResultHandler : ICreateSomeAsyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public CreateSomeAsyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeAsyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.CreateAsync()
            .ContinueAsync(this.accessor, async (e, a) => await a.AddEntityAsync(e, ct))
            .ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

public static class CreateSomeAsyncWithResultCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Es;

public partial class CreateSomeAsyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeAsyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Task<Result<Some>> CreateAsync()
    {
        WasValidated();
        Result<Some> result = new Some { Id = 1, Name = Name };
        return Task.FromResult(result);
    }
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Es;

public interface ICreateSomeAsyncWithResultHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeAsyncWithResult command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Es;

namespace Tests.Scenarios.Es.Internals;

public class CreateSomeAsyncWithResultHandler : ICreateSomeAsyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public CreateSomeAsyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeAsyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.CreateAsync()
            .ContinueAsync(this.accessor, async (e, a) => await a.AddEntityAsync(e, ct))
            .ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

""";
}