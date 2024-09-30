using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.Es;

public partial class CreateSomeWithDecoratorsSyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeWithDecoratorsSyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Result<Some> Create()
    {
        // WasValidated(); // somente para testes
        return new Some { Id = 1, Name = Name };
    }
}

public interface ICreateSomeWithDecoratorsSyncWithResultHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsSyncWithResult command, CancellationToken ct);
}

public class CreateSomeWithDecoratorsSyncWithResultHandler : ICreateSomeWithDecoratorsSyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<CreateSomeWithDecoratorsSyncWithResult, Result<Some>>> decorators;

    public CreateSomeWithDecoratorsSyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<CreateSomeWithDecoratorsSyncWithResult, Result<Some>>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsSyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<CreateSomeWithDecoratorsSyncWithResult, Result<Some>>(
            this.decorators,
            () => Task.FromResult(command.Create()),
            command,
            ct);

        return await decoratorsMediator.NextAsync()
            .ContinueAsync(this.accessor, async (e, a) => await a.AddEntityAsync(e, ct))
            .ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

public static class CreateSomeWithDecoratorsSyncWithResultCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Es;

public partial class CreateSomeWithDecoratorsSyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeWithDecoratorsSyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
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

public interface ICreateSomeWithDecoratorsSyncWithResultHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsSyncWithResult command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Es;

namespace Tests.Scenarios.Es.Internals;

public class CreateSomeWithDecoratorsSyncWithResultHandler : ICreateSomeWithDecoratorsSyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<CreateSomeWithDecoratorsSyncWithResult, Result<Some>>> decorators;

    public CreateSomeWithDecoratorsSyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<CreateSomeWithDecoratorsSyncWithResult, Result<Some>>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsSyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<CreateSomeWithDecoratorsSyncWithResult, Result<Some>>(
            this.decorators,
            () => Task.FromResult(command.Create()),
            command,
            ct);

        return await decoratorsMediator.NextAsync()
            .ContinueAsync(this.accessor, async (e, a) => await a.AddEntityAsync(e, ct))
            .ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

""";
}