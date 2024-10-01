using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Ds;

public class DoSomethingAsyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingAsyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public async Task<Result> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return Result.Ok();
    }
}

public interface IDoSomethingAsyncWithResultHandler
{
    public Task<Result> HandleAsync(DoSomethingAsyncWithResult command, CancellationToken ct);
}

public class DoSomethingAsyncWithResultHandler : IDoSomethingAsyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoSomethingAsyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result> HandleAsync(DoSomethingAsyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.DoAsync(this.accessor.Context).ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
    }
}

public static class DoSomethingAsyncWithResultCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

public class DoSomethingAsyncWithResult
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingAsyncWithResult>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
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

public interface IDoSomethingAsyncWithResultHandler
{
    public Task<Result> HandleAsync(DoSomethingAsyncWithResult command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

public class DoSomethingAsyncWithResultHandler : IDoSomethingAsyncWithResultHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoSomethingAsyncWithResultHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result> HandleAsync(DoSomethingAsyncWithResult command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.DoAsync(this.accessor.Context).ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
    }
}

""";
}