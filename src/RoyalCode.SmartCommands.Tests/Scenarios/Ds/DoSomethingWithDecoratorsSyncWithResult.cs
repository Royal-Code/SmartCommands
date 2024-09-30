using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Coreum.NewCommands.Tests.Scenarios.Ds;

public class DoSomethingWithDecoratorsSyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithDecoratorsSyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Result Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return Result.Ok();
    }
}

public interface IDoSomethingWithDecoratorsSyncWithResultHandler
{
    public Task<Result> HandleAsync(DoSomethingWithDecoratorsSyncWithResult command, CancellationToken ct);
}

public class DoSomethingWithDecoratorsSyncWithResultHandler : IDoSomethingWithDecoratorsSyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<DoSomethingWithDecoratorsSyncWithResult, Result>> decorators;

    public DoSomethingWithDecoratorsSyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<DoSomethingWithDecoratorsSyncWithResult, Result>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result> HandleAsync(DoSomethingWithDecoratorsSyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<DoSomethingWithDecoratorsSyncWithResult, Result>(
            this.decorators,
            () => Task.FromResult(command.Do(this.accessor.Context)),
            command,
            ct);

        return await decoratorsMediator.NextAsync().ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
    }
}

public static class DoSomethingWithDecoratorsSyncWithResultCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

public class DoSomethingWithDecoratorsSyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithDecoratorsSyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Result Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return Result.Ok();
    }
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Ds;

public interface IDoSomethingWithDecoratorsSyncWithResultHandler
{
    public Task<Result> HandleAsync(DoSomethingWithDecoratorsSyncWithResult command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

public class DoSomethingWithDecoratorsSyncWithResultHandler : IDoSomethingWithDecoratorsSyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<DoSomethingWithDecoratorsSyncWithResult, Result>> decorators;

    public DoSomethingWithDecoratorsSyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<DoSomethingWithDecoratorsSyncWithResult, Result>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result> HandleAsync(DoSomethingWithDecoratorsSyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<DoSomethingWithDecoratorsSyncWithResult, Result>(
            this.decorators,
            () => Task.FromResult(command.Do(this.accessor.Context)),
            command,
            ct);

        return await decoratorsMediator.NextAsync().ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
    }
}

""";
}