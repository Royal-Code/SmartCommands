using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartProblems;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Generators;

public class AddServicesTests
{

    [Fact]
    public void AddHandlersServices_Must_Generate_AddServicesMethod()
    {
        Util.Compile(Code.Command, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        Assert.Equal(Code.Interface, generatedInterface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        Assert.Equal(Code.Handler, generatedHandler);

        var generatedPartial = output.SyntaxTrees.Skip(3).FirstOrDefault()?.ToString();
        Assert.Equal(Code.AddServices, generatedPartial);

        Assert.Equal(4, output.SyntaxTrees.Count());
    }

    [Fact]
    public void AddHandlersServices_WithDbContext_Must_Generate_AddServicesMethod()
    {
        Util.Compile(CodeWithDbContext.Command, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        Assert.Equal(CodeWithDbContext.Interface, generatedInterface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        Assert.Equal(CodeWithDbContext.Handler, generatedHandler);

        var generatedPartial = output.SyntaxTrees.Skip(3).FirstOrDefault()?.ToString();
        Assert.Equal(CodeWithDbContext.AddServices, generatedPartial);

        Assert.Equal(4, output.SyntaxTrees.Count());
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

file static class CodeWithDbContext
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
    [Command, WithDbContext]
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
    public Task<Result> HandleAsync(MyCommand command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.AddServicesTests;

namespace Tests.Scenarios.AddServicesTests.Internals;

public class MyCommandHandler<TContext> : IMyCommandHandler
    where TContext : DbContext
{
    private readonly IUnitOfWorkAccessor<TContext> accessor;

    public MyCommandHandler(IUnitOfWorkAccessor<TContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result> HandleAsync(MyCommand command, CancellationToken ct)
    {
        await this.accessor.BeginAsync(ct);

        return await command.Do().ContinueAsync(this.accessor, static async (a, ct) => await a.CompleteAsync(ct), ct);
    }
}

""";

    public const string AddServices =
"""
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Scenarios.AddServicesTests.Internals;

namespace Tests.Scenarios.AddServicesTests;

public static partial class CoreumServiceCollectionExtensions
{
    public static void AddCoreumHandlersServices<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddTransient<IMyCommandHandler, MyCommandHandler<TContext>>();
    }
}

""";
}