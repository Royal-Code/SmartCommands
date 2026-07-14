using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Hs;

public class Tests
{
    [Theory]
    [InlineData(CodeHs.Command1, CodeHs.Interface1, CodeHs.Handler1, CodeHs.AddServices1, CodeHs.ApiHandlers1)]
    [InlineData(CodeHs.Command2, CodeHs.Interface2, CodeHs.Handler2, CodeHs.AddServices2, CodeHs.ApiHandlers2)]
    [InlineData(CodeHs.Command3, CodeHs.Interface1, CodeHs.Handler1, CodeHs.AddServices1, CodeHs.ApiHandlers1)]
    //[InlineData(CodeHs.Command4, CodeHs.Interface4, CodeHs.Handler4)]
    //[InlineData(CodeHs.Command5, CodeHs.Interface5, CodeHs.Handler5)]
    //[InlineData(CodeHs.Command6, CodeHs.Interface6, CodeHs.Handler6)]
    //[InlineData(CodeHs.Command7, CodeHs.Interface7, CodeHs.Handler7)]
    public void HsTests(string commandCode, string interfaceCode, string handlerCode, string addServicesCode, string apiHandlersCode)
    {
        Util.Compile(commandCode, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        Assert.Equal(Util.GeneratedCode(interfaceCode), generatedInterface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        Assert.Equal(Util.GeneratedCode(handlerCode), generatedHandler);

        var generatedAddServices = output.SyntaxTrees.Skip(3).FirstOrDefault()?.ToString();
        Assert.Equal(Util.GeneratedCode(addServicesCode), generatedAddServices);

        var generatedApiHandlers = output.SyntaxTrees.Skip(4).FirstOrDefault()?.ToString();
        Assert.Equal(Util.GeneratedCode(apiHandlersCode), generatedApiHandlers);
    }
}

file static class CodeHs
{
    public const string Command1 =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Hs;

[MapGroup("api/some")]
[MapPost("/", "create some")]
public class CreateSome
{
    public int Value { get; set; }

    [Command]
    internal Result Execute()
    {
        return Result.Ok();
    }
}

[AddHandlersServices(""), MapApiHandlers, WithOpenApi]
public static partial class ProgramExtensions
{ }
""";

    public const string Interface1 =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Hs;

public interface ICreateSomeHandler
{
    public Result Handle(CreateSome command);
}

""";

    public const string Handler1 =
"""
using RoyalCode.SmartProblems;
using Tests.Scenarios.Hs;

namespace Tests.Scenarios.Hs.Internals;

public class CreateSomeHandler : ICreateSomeHandler
{
    public Result Handle(CreateSome command)
    {
        return command.Execute();
    }
}

""";

    public const string AddServices1 =
"""
using Microsoft.Extensions.DependencyInjection;
using Tests.Scenarios.Hs.Internals;

namespace Tests.Scenarios.Hs;

public static partial class ProgramExtensions
{
    public static void AddHandlersServices(this IServiceCollection services)
    {
        services.AddTransient<ICreateSomeHandler, CreateSomeHandler>();
    }
}

""";

    public const string ApiHandlers1 =
"""
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.HttpResults;

namespace Tests.Scenarios.Hs;

public static partial class MapApiSomeApi
{
    public static RouteGroupBuilder MapApiSomeGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/some");

        group.MapPost("/", CreateSomeHandle)
            .WithName("create some")
            .WithOpenApi();

        return group;
    }

    private static OkMatch CreateSomeHandle(
        ICreateSomeHandler handler, 
        CreateSome? command)
    {
        if (command is null)
            return Problems.InvalidParameter("The request body is required.");

        var result = handler.Handle(command);
        return result;
    }
}

""";

    public const string Command3 =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Hs;

[MapGroup("api/some")]
[MapPost("/", "create some")]
public class CreateSome
{
    public CreateSome(int value)
    {
        Value = value;
    }

    public int Value { get; }

    [Command]
    internal Result Execute()
    {
        return Result.Ok();
    }
}

[AddHandlersServices(""), MapApiHandlers, WithOpenApi]
public static partial class ProgramExtensions
{ }
""";

    public const string Command2 =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Hs;

[MapGroup("api/some")]
[MapPost("/", "create some")]
[MapCreatedRoute("{0}", "Id")]
public class CreateSome
{
    public int Value { get; set; }

    [Command, ProduceNewEntity, WithUnitOfWork<AppDbContext>]
    internal Some Execute(AppDbContext db)
    {
        return new Some()
        {
            Value = Value,
            Active = true
        };
    }
}

[AddHandlersServices(""), MapApiHandlers, WithOpenApi]
public static partial class ProgramExtensions
{ }
""";

    public const string Interface2 =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Hs;

public interface ICreateSomeHandler
{
    public Task<Result<Some>> HandleAsync(CreateSome command, CancellationToken ct);
}

""";

    public const string Handler2 =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Hs;

namespace Tests.Scenarios.Hs.Internals;

public class CreateSomeHandler : ICreateSomeHandler
{
    private readonly IUnitOfWorkAccessor<AppDbContext> accessor;

    public CreateSomeHandler(IUnitOfWorkAccessor<AppDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Some>> HandleAsync(CreateSome command, CancellationToken ct)
    {
        await this.accessor.BeginAsync(ct);

        var commandResult = command.Execute(this.accessor.Context);

        await this.accessor.AddEntityAsync(commandResult, ct);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

""";

    public const string AddServices2 =
"""
using Microsoft.Extensions.DependencyInjection;
using Tests.Scenarios.Hs.Internals;

namespace Tests.Scenarios.Hs;

public static partial class ProgramExtensions
{
    public static void AddHandlersServices(this IServiceCollection services)
    {
        services.AddTransient<ICreateSomeHandler, CreateSomeHandler>();
    }
}

""";

    public const string ApiHandlers2 =
"""
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.HttpResults;

namespace Tests.Scenarios.Hs;

public static partial class MapApiSomeApi
{
    public static RouteGroupBuilder MapApiSomeGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/some");

        group.MapPost("/", CreateSomeHandleAsync)
            .WithName("create some")
            .WithOpenApi();

        return group;
    }

    private static async Task<CreatedMatch<Some>> CreateSomeHandleAsync(
        ICreateSomeHandler handler, 
        CreateSome? command, 
        CancellationToken ct)
    {
        if (command is null)
            return Problems.InvalidParameter("The request body is required.");

        var result = await handler.HandleAsync(command, ct);
        return result.CreatedMatch(v => $"api/some/{v.Id}");
    }
}

""";

    // Adicione os comandos, interfaces e handlers dos cen�rios 3 a 7 conforme o padr�o acima.
}
