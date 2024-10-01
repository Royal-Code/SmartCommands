using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Ds;

public class DoSomethingWithDecoratorsSync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithDecoratorsSync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Some Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return some;
    }
}

public interface IDoSomethingWithDecoratorsSyncHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsSync command, CancellationToken ct);
}

public class DoSomethingWithDecoratorsSyncHandler : IDoSomethingWithDecoratorsSyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<DoSomethingWithDecoratorsSync, Some>> decorators;

    public DoSomethingWithDecoratorsSyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<DoSomethingWithDecoratorsSync, Some>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsSync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<DoSomethingWithDecoratorsSync, Some>(
            this.decorators,
            () => Task.FromResult(command.Do(this.accessor.Context)),
            command,
            ct);

        var commandResult = await decoratorsMediator.NextAsync();

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

public static class DoSomethingWithDecoratorsSyncCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

public class DoSomethingWithDecoratorsSync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithDecoratorsSync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Some Do(AppDbContext db)
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

public interface IDoSomethingWithDecoratorsSyncHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsSync command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

public class DoSomethingWithDecoratorsSyncHandler : IDoSomethingWithDecoratorsSyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<DoSomethingWithDecoratorsSync, Some>> decorators;

    public DoSomethingWithDecoratorsSyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<DoSomethingWithDecoratorsSync, Some>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingWithDecoratorsSync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<DoSomethingWithDecoratorsSync, Some>(
            this.decorators,
            () => Task.FromResult(command.Do(this.accessor.Context)),
            command,
            ct);

        var commandResult = await decoratorsMediator.NextAsync();

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

""";
}