using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Coreum.NewCommands.Tests.Scenarios.Ds;

public class DoSomethingWithDecoratorsAsync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithDecoratorsAsync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public async Task<Some> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return some;
    }
}

public interface IDoSomethingWithDecoratorsAsyncHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsAsync command, CancellationToken ct);
}

public class DoSomethingWithDecoratorsAsyncHandler : IDoSomethingWithDecoratorsAsyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<DoSomethingWithDecoratorsAsync, Some>> decorators;

    public DoSomethingWithDecoratorsAsyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<DoSomethingWithDecoratorsAsync, Some>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsAsync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<DoSomethingWithDecoratorsAsync, Some>(
            this.decorators,
            async () => await command.DoAsync(this.accessor.Context),
            command,
            ct);

        var commandResult = await decoratorsMediator.NextAsync();

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

public static class DoSomethingWithDecoratorsAsyncCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

public class DoSomethingWithDecoratorsAsync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithDecoratorsAsync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public async Task<Some> DoAsync(AppDbContext db)
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

public interface IDoSomethingWithDecoratorsAsyncHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsAsync command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

public class DoSomethingWithDecoratorsAsyncHandler : IDoSomethingWithDecoratorsAsyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<DoSomethingWithDecoratorsAsync, Some>> decorators;

    public DoSomethingWithDecoratorsAsyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<DoSomethingWithDecoratorsAsync, Some>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsAsync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<DoSomethingWithDecoratorsAsync, Some>(
            this.decorators,
            async () => await command.DoAsync(this.accessor.Context),
            command,
            ct);

        var commandResult = await decoratorsMediator.NextAsync();

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

""";
}