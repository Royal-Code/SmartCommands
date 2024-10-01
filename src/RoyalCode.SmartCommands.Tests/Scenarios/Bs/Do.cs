using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Bs;

public class Do
{
    public string? Name { get; set; }

    [Command]
    public Result Get() => Result.Ok();
}

public interface IDoHandler
{
    public Result Handle(Do command);
}

public class DoHandler : IDoHandler
{
    public Result Handle(Do command)
    {
        return command.Get();
    }
}