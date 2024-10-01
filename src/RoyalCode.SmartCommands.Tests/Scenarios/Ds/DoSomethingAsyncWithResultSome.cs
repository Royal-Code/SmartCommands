using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Ds;

public class DoSomethingAsyncWithResultSome
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingAsyncWithResultSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public async Task<Result<Some>> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return some;
    }
}

public interface IDoSomethingAsyncWithResultSomeHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingAsyncWithResultSome command, CancellationToken ct);
}

public class DoSomethingAsyncWithResultSomeHandler : IDoSomethingAsyncWithResultSomeHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoSomethingAsyncWithResultSomeHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingAsyncWithResultSome command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.DoAsync(this.accessor.Context).ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

public static class DoSomethingAsyncWithResultSomeCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

public class DoSomethingAsyncWithResultSome
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingAsyncWithResultSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
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

public interface IDoSomethingAsyncWithResultSomeHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingAsyncWithResultSome command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

public class DoSomethingAsyncWithResultSomeHandler : IDoSomethingAsyncWithResultSomeHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoSomethingAsyncWithResultSomeHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingAsyncWithResultSome command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.DoAsync(this.accessor.Context).ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

""";
}