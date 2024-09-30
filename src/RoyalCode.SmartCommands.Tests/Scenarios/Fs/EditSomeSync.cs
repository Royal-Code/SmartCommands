using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.Fs;

public partial class EditSomeSync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<EditSomeSync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, EditEntity<Some, int>, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Result Edit(Some some)
    {
        // WasValidated(); // somente para testes
        some.Name = Name;
        return Result.Ok();
    }
}

public interface IEditSomeSyncHandler
{
    public Task<Result> HandleAsync(int someId, EditSomeSync command, CancellationToken ct);
}

public class EditSomeSyncHandler : IEditSomeSyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public EditSomeSyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result> HandleAsync(int someId, EditSomeSync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        Problems? notFoundProblems;

        var someEntry = await this.accessor.FindEntityAsync<Some, int>(someId, ct);
        if (someEntry.NotFound(out notFoundProblems))
            return notFoundProblems;
        var some = someEntry.Entity;

        return await command.Edit(some).ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
    }
}

public static class EditSomeSyncCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Fs;

public partial class EditSomeSync
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<EditSomeSync>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, EditEntity<Some, int>, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Result Edit(Some some)
    {
        WasValidated();
        some.Name = Name;
        return Result.Ok();
    }
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Fs;

public interface IEditSomeSyncHandler
{
    public Task<Result> HandleAsync(int someId, EditSomeSync command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Fs;

namespace Tests.Scenarios.Fs.Internals;

public class EditSomeSyncHandler : IEditSomeSyncHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public EditSomeSyncHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result> HandleAsync(int someId, EditSomeSync command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        Problems? notFoundProblems;

        var someEntry = await this.accessor.FindEntityAsync<Some, int>(someId, ct);
        if (someEntry.NotFound(out notFoundProblems))
            return notFoundProblems;
        var some = someEntry.Entity;

        return await command.Edit(some).ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
    }
}

""";
}