using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Gs;

public class DoWithParameters
{
    public int Value { get; set; }

    [Command]
    internal Result<int> Plus([WithParameter] int other)
    {
        return Value + other;
    }
}

public interface IDoWithParametersHandler
{
    public Result<int> Handle(DoWithParameters command, int other);
}

public class DoWithParametersHandler : IDoWithParametersHandler
{
    public Result<int> Handle(DoWithParameters command, int other)
    {
        return command.Plus(other);
    }
}

public static class DoWithParametersCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Gs;

public class DoWithParameters
{
    public int Value { get; set; }

    [Command]
    internal Result<int> Plus([WithParameter] int other)
    {
        return Value + other;
    }
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Gs;

public interface IDoWithParametersHandler
{
    public Result<int> Handle(DoWithParameters command, int other);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartProblems;
using Tests.Scenarios.Gs;

namespace Tests.Scenarios.Gs.Internals;

public class DoWithParametersHandler : IDoWithParametersHandler
{
    public Result<int> Handle(DoWithParameters command, int other)
    {
        return command.Plus(other);
    }
}

""";
}