using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Tests.Generators;

public class AddServicesTests
{

    [Fact]
    public void AddHandlersServices_Must_Generate_AddServicesMethod()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(Code.Interface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(Code.Handler);

        var generatedPartial = output.SyntaxTrees.Skip(3).FirstOrDefault()?.ToString();
        generatedPartial.Should().Be(Code.AddServices);

        output.SyntaxTrees.Count().Should().Be(4);
    }
}

// My code
public class MyCommand
{
    [Command]
    public Result Do() => Result.Ok();
}

// My code
[AddHandlersServices("Coreum")]
file static partial class CoreumServiceCollectionExtensions { }

// Generated code
file static partial class CoreumServiceCollectionExtensions
{
    public static void AddCoreumHandlersServices(this IServiceCollection services)
    {
        services.AddTransient<IMyCommandHandler, MyCommandHandler>();
    }
}

// Generated code
public interface IMyCommandHandler
{
    public Result Handle(MyCommand command);
}

// Generated code
public class MyCommandHandler : IMyCommandHandler
{
    public Result Handle(MyCommand command)
    {
        return command.Do();
    }
}

file static class Code
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.AddServicesTests;

// My code
public class MyCommand
{
    [Command]
    public Result Do() => Result.Ok();
}

// My code
[AddHandlersServices("Coreum")]
public static partial class CoreumServiceCollectionExtensions { }
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.AddServicesTests;

public interface IMyCommandHandler
{
    public Result Handle(MyCommand command);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartProblems;
using Tests.Scenarios.AddServicesTests;

namespace Tests.Scenarios.AddServicesTests.Internals;

public class MyCommandHandler : IMyCommandHandler
{
    public Result Handle(MyCommand command)
    {
        return command.Do();
    }
}

""";

    public const string AddServices =
"""
using Microsoft.Extensions.DependencyInjection;
using Tests.Scenarios.AddServicesTests.Internals;

namespace Tests.Scenarios.AddServicesTests;

public static partial class CoreumServiceCollectionExtensions
{
    public static void AddCoreumHandlersServices(this IServiceCollection services)
    {
        services.AddTransient<IMyCommandHandler, MyCommandHandler>();
    }
}

""";
}