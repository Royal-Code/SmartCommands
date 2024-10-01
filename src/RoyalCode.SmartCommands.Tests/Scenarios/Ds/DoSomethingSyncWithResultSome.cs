using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Ds;

/// <summary>
/// Cenários síncronos sem decoradores
/// Comando que retorna um Result de Some
/// </summary>
public class DoSomethingSyncWithResultSome
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingSyncWithResultSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Result<Some> Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return some;
    }
}

public interface IDoSomethingSyncWithResultSomeHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingSyncWithResultSome command, CancellationToken ct);
}

public class DoSomethingSyncWithResultSomeHandler : IDoSomethingSyncWithResultSomeHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoSomethingSyncWithResultSomeHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingSyncWithResultSome command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.Do(this.accessor.Context).ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}


public static class DoSomethingSyncWithResultSomeCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

public class DoSomethingSyncWithResultSome
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingSyncWithResultSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
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

public interface IDoSomethingSyncWithResultSomeHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingSyncWithResultSome command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

public class DoSomethingSyncWithResultSomeHandler : IDoSomethingSyncWithResultSomeHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoSomethingSyncWithResultSomeHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingSyncWithResultSome command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.Do(this.accessor.Context).ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

""";
}