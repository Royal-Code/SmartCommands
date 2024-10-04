using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.HttpResults;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Is;

[MapGroup("some")]
[MapPost("", "create-some"), MapIdResultValue]
public class CreateSome
{
    public int Value { get; set; }

    public string Name { get; set; }

    [Command]
    internal Task<Result<Some>> Execute()
    {
        Result<Some> result = new Some()
        {
            Value = Value,
            Name = Name,
            Active = true
        };

        return Task.FromResult(result);
    }
}

public interface ICreateSomeHandler
{
    Task<Result<Some>> HandleAsync(CreateSome command, CancellationToken ct);
}

public static partial class MapSomeApi
{
    public static RouteGroupBuilder MapSomeGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("some");

        group.MapPost("", CreateSomeHandleAsync)
            .WithName("create-some")
            .WithOpenApi();

        return group;
    }

    [ProduceProblems(ProblemCategory.InvalidParameter)]
    private static async Task<OkMatch<int>> CreateSomeHandleAsync(
        ICreateSomeHandler handler,
        CreateSome command,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.Map(v => v.Id);
    }
}

public static class CreateSomeMapIdCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Tests.Scenarios.Is;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using Microsoft.AspNetCore.Routing;

namespace Tests.Scenarios.Is;

[MapGroup("some")]
[MapPost("", "create-some"), MapIdResultValue]
public class CreateSome
{
    public int Value { get; set; }

    public string Name { get; set; }

    [Command]
    internal Task<Result<Some>> Execute()
    {
        Result<Some> result = new Some()
        {
            Value = Value,
            Name = Name,
            Active = true
        };

        return Task.FromResult(result);
    }
}

[MapApiHandlers]
public static partial class VaultApis { }
""";

    public const string Map =
"""
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.HttpResults;

namespace Tests.Scenarios.Is;

public static partial class MapSomeApi
{
    public static RouteGroupBuilder MapSomeGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("some");

        group.MapPost("", CreateSomeHandleAsync)
            .WithName("create-some")
            .WithOpenApi();

        return group;
    }

    private static async Task<OkMatch<int>> CreateSomeHandleAsync(
        ICreateSomeHandler handler, 
        CreateSome command, 
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.Map(v => v.Id);
    }
}

""";
}