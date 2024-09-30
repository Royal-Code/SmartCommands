using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.Es;

public partial class CreateSomeWithDecoratorsAsync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeWithDecoratorsAsync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Task<Some> CreateAsync()
    {
        // WasValidated(); // somente para testes
        var some = new Some { Id = 1, Name = Name };
        return Task.FromResult(some);
    }
}

public interface ICreateSomeWithDecoratorsAsyncHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsAsync command, CancellationToken ct);
}

public class CreateSomeWithDecoratorsAsyncHandler : ICreateSomeWithDecoratorsAsyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<CreateSomeWithDecoratorsAsync, Some>> decorators;

    public CreateSomeWithDecoratorsAsyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<CreateSomeWithDecoratorsAsync, Some>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsAsync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<CreateSomeWithDecoratorsAsync, Some>(
            this.decorators,
            async () => await command.CreateAsync(),
            command,
            ct);

        var commandResult = await decoratorsMediator.NextAsync();

        await this.accessor.AddEntityAsync(commandResult, ct);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

public static class CreateSomeWithDecoratorsAsyncCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Es;

public partial class CreateSomeWithDecoratorsAsync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateSomeWithDecoratorsAsync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, ProduceNewEntity, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Task<Some> CreateAsync()
    {
        WasValidated();
        var some = new Some { Id = 1, Name = Name };
        return Task.FromResult(some);
    }
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Es;

public interface ICreateSomeWithDecoratorsAsyncHandler
{
    public Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsAsync command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Es;

namespace Tests.Scenarios.Es.Internals;

public class CreateSomeWithDecoratorsAsyncHandler : ICreateSomeWithDecoratorsAsyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;
    private readonly IEnumerable<IDecorator<CreateSomeWithDecoratorsAsync, Some>> decorators;

    public CreateSomeWithDecoratorsAsyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor, IEnumerable<IDecorator<CreateSomeWithDecoratorsAsync, Some>> decorators)
    {
        this.accessor = accessor;
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(CreateSomeWithDecoratorsAsync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var decoratorsMediator = new Mediator<CreateSomeWithDecoratorsAsync, Some>(
            this.decorators,
            async () => await command.CreateAsync(),
            command,
            ct);

        var commandResult = await decoratorsMediator.NextAsync();

        await this.accessor.AddEntityAsync(commandResult, ct);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

""";
}