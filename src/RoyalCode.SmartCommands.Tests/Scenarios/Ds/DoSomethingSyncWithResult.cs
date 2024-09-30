using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Coreum.NewCommands.Tests.Scenarios.Ds;

/// <summary>
/// Cenários síncronos sem decoradores
/// Comando que retorna um Result
/// </summary>
public class DoSomethingSyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingSyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Result Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return Result.Ok();
    }
}

public interface IDoSomethingSyncWithResultHandler
{
    public Task<Result> HandleAsync(DoSomethingSyncWithResult command, CancellationToken ct);
}

public class DoSomethingSyncWithResultHandler : IDoSomethingSyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoSomethingSyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result> HandleAsync(DoSomethingSyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.Do(this.accessor.Context).ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
    }
}

public static class DoSomethingSyncWithResultCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

public class DoSomethingSyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingSyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
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

public interface IDoSomethingSyncWithResultHandler
{
    public Task<Result> HandleAsync(DoSomethingSyncWithResult command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

public class DoSomethingSyncWithResultHandler : IDoSomethingSyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoSomethingSyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result> HandleAsync(DoSomethingSyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.Do(this.accessor.Context).ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
    }
}

""";
}