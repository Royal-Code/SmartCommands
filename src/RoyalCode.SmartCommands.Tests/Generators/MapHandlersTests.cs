using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using RoyalCode.Entities;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.HttpResults;

namespace RoyalCode.SmartCommands.Tests.Generators;

// My code
public class My : Entity<int> 
{
    public My(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; set; }
}

// My code
[MapPost("", "my-create")]
public class CreateMy
{
    public string? Name { get; set; }

    [Command, ProduceNewEntity, WithUnitOfWork<DbContext>]
    public My Do() => new My(Name!);
}

// generate
public interface ICreateMyHandler
{
    public Task<Result<My>> HandleAsync(CreateMy command, CancellationToken ct);
}

[MapApiHandlers] // my-code
public static partial class Extensions
{
    // my-code
    public static void MapSomething(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("something");
        group.MapMy();
    }

    // generate
    internal static void MapMy(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("", MapCreateMyHandler)
            .WithName("my-create");
    }

    // generate
    private static async Task<OkMatch<My>> MapCreateMyHandler(
        [FromServices] ICreateMyHandler createMyHandler,
        [FromBody] CreateMy command,
        CancellationToken ct)
    {
        return await createMyHandler.HandleAsync(command, ct);
    }
}
