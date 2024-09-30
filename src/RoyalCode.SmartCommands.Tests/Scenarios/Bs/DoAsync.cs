using RoyalCode.SmartProblems;

namespace Coreum.NewCommands.Tests.Scenarios.Bs;

public class DoAsync
{
    public string? Name { get; set; }

    [Command]
    public Task<Result> GetAsync() => Task.FromResult(Result.Ok());
}

public interface IDoAsyncHandler
{
    public Task<Result> HandleAsync(DoAsync command, CancellationToken ct);
}

public class DoAsyncHandler : IDoAsyncHandler
{
    public async Task<Result> HandleAsync(DoAsync command, CancellationToken ct)
    {
        return await command.GetAsync();
    }
}