using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.Es;

public partial class CreateSomeWithDecoratorsAsyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeWithDecoratorsAsyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Task<Result<Some>> CreateAsync()
    {
        // WasValidated(); // somente para testes
        Result<Some> result = new Some { Id = 1, Name = Name };
        return Task.FromResult(result);
    }
}

public interface ICreateSomeWithDecoratorsAsyncWithResultHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsAsyncWithResult command, CancellationToken ct);
}

public class CreateSomeWithDecoratorsAsyncWithResultHandler : ICreateSomeWithDecoratorsAsyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<CreateSomeWithDecoratorsAsyncWithResult, Result<Some>>> decorators;

    public CreateSomeWithDecoratorsAsyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<CreateSomeWithDecoratorsAsyncWithResult, Result<Some>>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsAsyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<CreateSomeWithDecoratorsAsyncWithResult, Result<Some>>(
            this.decorators,
            async () => await command.CreateAsync(),
            command,
            ct);

        return await decoratorsMediator.NextAsync()
            .ContinueAsync(this.accessor, async (e, a) => await a.AddEntityAsync(e, ct))
            .ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

public static class CreateSomeWithDecoratorsAsyncWithResultCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Es;

public partial class CreateSomeWithDecoratorsAsyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeWithDecoratorsAsyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
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

public interface ICreateSomeWithDecoratorsAsyncWithResultHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsAsyncWithResult command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Es;

namespace Tests.Scenarios.Es.Internals;

public class CreateSomeWithDecoratorsAsyncWithResultHandler : ICreateSomeWithDecoratorsAsyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<CreateSomeWithDecoratorsAsyncWithResult, Result<Some>>> decorators;

    public CreateSomeWithDecoratorsAsyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<CreateSomeWithDecoratorsAsyncWithResult, Result<Some>>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsAsyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<CreateSomeWithDecoratorsAsyncWithResult, Result<Some>>(
            this.decorators,
            async () => await command.CreateAsync(),
            command,
            ct);

        return await decoratorsMediator.NextAsync()
            .ContinueAsync(this.accessor, async (e, a) => await a.AddEntityAsync(e, ct))
            .ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

""";
}