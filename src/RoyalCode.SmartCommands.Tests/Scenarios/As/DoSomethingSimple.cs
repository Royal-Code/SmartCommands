namespace RoyalCode.SmartCommands.Tests.Scenarios.As;

public class DoSomethingSimple
{
    public string? Name { get; set; }

    [Command]
    public Some Get() => new Some(Name ?? throw new Exception("Bad Name"));
}

public interface IDoSomethingSimpleHandler
{
    public Some Handle(DoSomethingSimple model);
}

public class DoSomethingSimpleHandler : IDoSomethingSimpleHandler
{
    public Some Handle(DoSomethingSimple model)
    {
        return model.Get();
    }
}