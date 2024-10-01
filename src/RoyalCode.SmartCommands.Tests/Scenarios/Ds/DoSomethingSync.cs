using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Ds;

/// <summary>
/// Cenários síncronos sem decoradores
/// Comando que retorna um objeto
/// </summary>
public class DoSomethingSync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingSync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Some Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return some;
    }
}

public interface IDoSomethingSyncHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingSync command, CancellationToken ct);
}

public class DoSomethingSyncHandler : IDoSomethingSyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoSomethingSyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingSync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var commandResult = command.Do(this.accessor.Context);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

public static class DoSomethingSyncCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

public class DoSomethingSync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingSync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
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

public interface IDoSomethingSyncHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingSync command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

public class DoSomethingSyncHandler : IDoSomethingSyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoSomethingSyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingSync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var commandResult = command.Do(this.accessor.Context);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

""";
}