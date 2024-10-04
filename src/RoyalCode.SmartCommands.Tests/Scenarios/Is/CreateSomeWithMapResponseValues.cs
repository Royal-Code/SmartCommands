using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.HttpResults;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Is;

[MapGroup("some-with-map-response-values")]
[MapPost("", "create-some"), MapResponseValues("Id", "Name")]
public class CreateSomeWithMapResponseValues
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

public class CreateSomeWithMapResponseValuesResponse(int id, string name)
{
    int Id { get; } = id;

    string Name { get; } = name;
}

public interface ICreateSomeWithMapResponseValuesHandler
{
    Task<Result<Some>> HandleAsync(CreateSomeWithMapResponseValues command, CancellationToken ct);
}

public static partial class MapSomeWithMapResponseValuesApi
{
    public static RouteGroupBuilder MapSomeWithMapResponseValuesGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("some");

        group.MapPost("", CreateSomeWithMapResponseValuesHandleAsync)
            .WithName("create-some")
            .WithOpenApi();

        return group;
    }

    [ProduceProblems(ProblemCategory.InvalidParameter)]
    private static async Task<OkMatch<CreateSomeWithMapResponseValuesResponse>> CreateSomeWithMapResponseValuesHandleAsync(
        ICreateSomeWithMapResponseValuesHandler handler,
        CreateSomeWithMapResponseValues command,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.Map(v => new CreateSomeWithMapResponseValuesResponse(v.Id, v.Name));
    }
}

public static class CreateSomeWithMapResponseValuesCode
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
[MapPost("", "create-some"), MapResponseValues("Id", "Name")]
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

    private static async Task<OkMatch<CreateSomeResponse>> CreateSomeHandleAsync(
        ICreateSomeHandler handler, 
        CreateSome command, 
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.Map(v => new CreateSomeResponse(v.Id, v.Name));
    }
}

""";

    public const string ResponseModel =
"""

namespace Tests.Scenarios.Is;

public partial class CreateSomeResponse(int id, string name)
{
    int Id { get; } = id;

    string Name { get; } = name;
}

""";
}