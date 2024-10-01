using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Ds;

public class DoSomethingWithDecoratorsAsyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithDecoratorsAsyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public async Task<Result> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return Result.Ok();
    }
}

public interface IDoSomethingWithDecoratorsAsyncWithResultHandler
{
    public Task<Result> HandleAsync(DoSomethingWithDecoratorsAsyncWithResult command, CancellationToken ct);
}

public class DoSomethingWithDecoratorsAsyncWithResultHandler : IDoSomethingWithDecoratorsAsyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<DoSomethingWithDecoratorsAsyncWithResult, Result>> decorators;

    public DoSomethingWithDecoratorsAsyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<DoSomethingWithDecoratorsAsyncWithResult, Result>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result> HandleAsync(DoSomethingWithDecoratorsAsyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<DoSomethingWithDecoratorsAsyncWithResult, Result>(
            this.decorators,
            async () => await command.DoAsync(this.accessor.Context),
            command,
            ct);

        return await decoratorsMediator.NextAsync().ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
    }
}

public static class DoSomethingWithDecoratorsAsyncWithResultCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

public class DoSomethingWithDecoratorsAsyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithDecoratorsAsyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public async Task<Result> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return Result.Ok();
    }
}

""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Ds;

public interface IDoSomethingWithDecoratorsAsyncWithResultHandler
{
    public Task<Result> HandleAsync(DoSomethingWithDecoratorsAsyncWithResult command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

public class DoSomethingWithDecoratorsAsyncWithResultHandler : IDoSomethingWithDecoratorsAsyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<DoSomethingWithDecoratorsAsyncWithResult, Result>> decorators;

    public DoSomethingWithDecoratorsAsyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<DoSomethingWithDecoratorsAsyncWithResult, Result>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result> HandleAsync(DoSomethingWithDecoratorsAsyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<DoSomethingWithDecoratorsAsyncWithResult, Result>(
            this.decorators,
            async () => await command.DoAsync(this.accessor.Context),
            command,
            ct);

        return await decoratorsMediator.NextAsync().ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
    }
}

""";
}