using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Gs;

public class DoWithTwoParameters
{
    public int Value { get; set; }

    [Command, EditEntity<Some, int>, WithUnitOfWork<AppDbContext>]
    internal Result<int> Plus(Some some, [WithParameter] int other, [WithParameter] int another)
    {
        some.Value += Value + other + another;
        return some.Value;
    }
}

public interface IDoWithTwoParametersHandler
{
    public Task<Result<int>> HandleAsync(int someId, DoWithTwoParameters command, int other, int another, CancellationToken ct);
}

public class DoWithTwoParametersHandler : IDoWithTwoParametersHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoWithTwoParametersHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<int>> HandleAsync(int someId, DoWithTwoParameters command, int other, int another, CancellationToken ct)
    {
        await this.accessor.BeginAsync(ct);

        Problem? notFoundProblem;

        var someEntry = await this.accessor.FindEntityAsync<Some, int>(someId, ct);
        if (someEntry.NotFound(out notFoundProblem))
            return notFoundProblem;
        var some = someEntry.Entity;

        return await command.Plus(some, other, another).ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

public static class DoWithTwoParametersCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Gs
{
    using Tests.Scenarios.Gs.Models;

    public class DoWithTwoParameters
    {
        public int Value { get; set; }

        [Command, EditEntity<Some, int>, WithUnitOfWork<AppDbContext>]
        internal Result<int> Plus(Some some, [WithParameter] int other, [WithParameter] int another)
        {
            some.Value += Value + other + another;
            return some.Value;
        }
    }

}

namespace Tests.Scenarios.Gs.Models
{
    public class Some
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }
}

""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Gs;

public interface IDoWithTwoParametersHandler
{
    public Task<Result<int>> HandleAsync(int someId, DoWithTwoParameters command, int other, int another, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Gs;
using Tests.Scenarios.Gs.Models;

namespace Tests.Scenarios.Gs.Internals;

public class DoWithTwoParametersHandler : IDoWithTwoParametersHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public DoWithTwoParametersHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<int>> HandleAsync(int someId, DoWithTwoParameters command, int other, int another, CancellationToken ct)
    {
        await this.accessor.BeginAsync(ct);

        Problem? notFoundProblem;

        var someEntry = await this.accessor.FindEntityAsync<Some, int>(someId, ct);
        if (someEntry.NotFound(out notFoundProblem))
            return notFoundProblem;
        var some = someEntry.Entity;

        return await command.Plus(some, other, another).ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}

""";
}