namespace Coreum.NewCommands.Tests.Scenarios.As;

public class DoSomethingAsyncSimple
{
    public string? Name { get; set; }

    [Command]
    public Task<Some> GetAsync() => Task.FromResult(new Some(Name ?? throw new Exception("Bad Name")));
}

public interface IDoSomethingAsyncSimpleHandler
{
    public Task<Some> HandleAsync(DoSomethingAsyncSimple command);
}

public class DoSomethingAsyncSimpleHandler : IDoSomethingAsyncSimpleHandler
{
    public async Task<Some> HandleAsync(DoSomethingAsyncSimple command)
    {
        return await command.GetAsync();
    }
}