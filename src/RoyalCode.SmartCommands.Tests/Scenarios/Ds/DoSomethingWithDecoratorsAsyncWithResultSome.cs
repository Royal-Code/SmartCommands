using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Ds;

public class DoSomethingWithDecoratorsAsyncWithResultSome
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithDecoratorsAsyncWithResultSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public async Task<Result<Some>> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return some;
    }
}

public interface IDoSomethingWithDecoratorsAsyncWithResultSomeHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsAsyncWithResultSome command, CancellationToken ct);
}

public class DoSomethingWithDecoratorsAsyncWithResultSomeHandler : IDoSomethingWithDecoratorsAsyncWithResultSomeHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<DoSomethingWithDecoratorsAsyncWithResultSome, Result<Some>>> decorators;

    public DoSomethingWithDecoratorsAsyncWithResultSomeHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<DoSomethingWithDecoratorsAsyncWithResultSome, Result<Some>>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsAsyncWithResultSome command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<DoSomethingWithDecoratorsAsyncWithResultSome, Result<Some>>(
            this.decorators,
            async () => await command.DoAsync(this.accessor.Context),
            command,
            ct);

        return await decoratorsMediator.NextAsync().ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

public static class DoSomethingWithDecoratorsAsyncWithResultSomeCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

public class DoSomethingWithDecoratorsAsyncWithResultSome
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithDecoratorsAsyncWithResultSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public async Task<Result<Some>> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return some;
    }
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Ds;

public interface IDoSomethingWithDecoratorsAsyncWithResultSomeHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsAsyncWithResultSome command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

public class DoSomethingWithDecoratorsAsyncWithResultSomeHandler : IDoSomethingWithDecoratorsAsyncWithResultSomeHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<DoSomethingWithDecoratorsAsyncWithResultSome, Result<Some>>> decorators;

    public DoSomethingWithDecoratorsAsyncWithResultSomeHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<DoSomethingWithDecoratorsAsyncWithResultSome, Result<Some>>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsAsyncWithResultSome command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<DoSomethingWithDecoratorsAsyncWithResultSome, Result<Some>>(
            this.decorators,
            async () => await command.DoAsync(this.accessor.Context),
            command,
            ct);

        return await decoratorsMediator.NextAsync().ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

""";
}