namespace Coreum.NewCommands.Tests.Scenarios.Bs;


public record Some(string Name)
{
    public static Task<Some> Create(string name)
    {
        return Task.FromResult(new Some(name));
    }
}
