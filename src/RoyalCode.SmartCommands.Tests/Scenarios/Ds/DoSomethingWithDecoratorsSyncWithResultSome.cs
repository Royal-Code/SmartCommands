using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Coreum.NewCommands.Tests.Scenarios.Ds;

public class DoSomethingWithDecoratorsSyncWithResultSome
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithDecoratorsSyncWithResultSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Result<Some> Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return some;
    }
}

public interface IDoSomethingWithDecoratorsSyncWithResultSomeHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsSyncWithResultSome command, CancellationToken ct);
}

public class DoSomethingWithDecoratorsSyncWithResultSomeHandler : IDoSomethingWithDecoratorsSyncWithResultSomeHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<DoSomethingWithDecoratorsSyncWithResultSome, Result<Some>>> decorators;

    public DoSomethingWithDecoratorsSyncWithResultSomeHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<DoSomethingWithDecoratorsSyncWithResultSome, Result<Some>>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsSyncWithResultSome command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<DoSomethingWithDecoratorsSyncWithResultSome, Result<Some>>(
            this.decorators,
            () => Task.FromResult(command.Do(this.accessor.Context)),
            command,
            ct);

        return await decoratorsMediator.NextAsync().ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

public static class DoSomethingWithDecoratorsSyncWithResultSomeCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

public class DoSomethingWithDecoratorsSyncWithResultSome
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithDecoratorsSyncWithResultSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Result<Some> Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return some;
    }
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Ds;

public interface IDoSomethingWithDecoratorsSyncWithResultSomeHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsSyncWithResultSome command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

public class DoSomethingWithDecoratorsSyncWithResultSomeHandler : IDoSomethingWithDecoratorsSyncWithResultSomeHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<DoSomethingWithDecoratorsSyncWithResultSome, Result<Some>>> decorators;

    public DoSomethingWithDecoratorsSyncWithResultSomeHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<DoSomethingWithDecoratorsSyncWithResultSome, Result<Some>>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsSyncWithResultSome command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<DoSomethingWithDecoratorsSyncWithResultSome, Result<Some>>(
            this.decorators,
            () => Task.FromResult(command.Do(this.accessor.Context)),
            command,
            ct);

        return await decoratorsMediator.NextAsync().ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

""";
}