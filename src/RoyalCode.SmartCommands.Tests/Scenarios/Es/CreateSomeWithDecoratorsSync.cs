using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Es;

public partial class CreateSomeWithDecoratorsSync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeWithDecoratorsSync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Some Create()
    {
        // WasValidated(); // somente para testes
        return new Some { Id = 1, Name = Name };
    }
}

public interface ICreateSomeWithDecoratorsSyncHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsSync command, CancellationToken ct);
}

public class CreateSomeWithDecoratorsSyncHandler : ICreateSomeWithDecoratorsSyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<CreateSomeWithDecoratorsSync, Some>> decorators;

    public CreateSomeWithDecoratorsSyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<CreateSomeWithDecoratorsSync, Some>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsSync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<CreateSomeWithDecoratorsSync, Some>(
            this.decorators,
            () => Task.FromResult(command.Create()),
            command,
            ct);

        var commandResult = await decoratorsMediator.NextAsync();

        await this.accessor.AddEntityAsync(commandResult, ct);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

public static class CreateSomeWithDecoratorsSyncCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Es;

public partial class CreateSomeWithDecoratorsSync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeWithDecoratorsSync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Some Create()
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

public interface ICreateSomeWithDecoratorsSyncHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsSync command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Es;

namespace Tests.Scenarios.Es.Internals;

public class CreateSomeWithDecoratorsSyncHandler : ICreateSomeWithDecoratorsSyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<CreateSomeWithDecoratorsSync, Some>> decorators;

    public CreateSomeWithDecoratorsSyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<CreateSomeWithDecoratorsSync, Some>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsSync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<CreateSomeWithDecoratorsSync, Some>(
            this.decorators,
            () => Task.FromResult(command.Create()),
            command,
            ct);

        var commandResult = await decoratorsMediator.NextAsync();

        await this.accessor.AddEntityAsync(commandResult, ct);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

""";
}