using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Movies;
using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.SmartProblems.HttpResults;

namespace RoyalCode.SmartCommands.Demo;

public static partial class MapMoviesApi
{
    public static RouteGroupBuilder MapMoviesGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("movies");

        group.MapGet("{id:int}", FindReviewHandleAsync)
            .WithName("Get review details")
            .WithOpenApi();

        return group;
    }

    [ProduceProblems(ProblemCategory.NotFound)]
    private static async Task<OkMatch<ReviewDetails>> FindReviewHandleAsync(
        Id<Review, int> id, 
        IRepositoryAccessor<Review> accessor, 
        CancellationToken ct)
    {
        var findResult = await accessor.FindEntityAsync<ReviewDetails, int>(id, ct);
        if (findResult.NotFound(out var notfoundProblem))
            return notfoundProblem;

        return findResult.Entity;
    }
}
