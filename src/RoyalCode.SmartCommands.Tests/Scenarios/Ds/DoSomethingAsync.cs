using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Coreum.NewCommands.Tests.Scenarios.Ds;

/// <summary>
/// Cenários assíncronos sem decoradores
/// Comando que retorna um objeto
/// </summary>
public class DoSomethingAsync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingAsync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public async Task<Some> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return some;
    }
}

public interface IDoSomethingAsyncHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingAsync command, CancellationToken ct);
}

public class DoSomethingAsyncHandler : IDoSomethingAsyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoSomethingAsyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingAsync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var commandResult = await command.DoAsync(this.accessor.Context);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

public static class DoSomethingAsyncCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

public class DoSomethingAsync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingAsync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
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

public interface IDoSomethingAsyncHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingAsync command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

public class DoSomethingAsyncHandler : IDoSomethingAsyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoSomethingAsyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingAsync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var commandResult = await command.DoAsync(this.accessor.Context);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

""";
}