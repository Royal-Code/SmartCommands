using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartCommands.Demo.Commands.Lojas;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.HttpResults;

namespace RoyalCode.SmartCommands.Demo;

public static partial class MapLojasApi
{
    public static RouteGroupBuilder MapLojasGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("lojas");

        group.MapPost("/", CriarLojaHandleAsync)
            .WithName("loja-criar");

        return group;
    }

    [ProduceProblems(ProblemCategory.InvalidParameter)]
    private static async Task<CreatedMatch<CriarLojaResponse>> CriarLojaHandleAsync(
        ICriarLojaHandler handler, 
        CriarLoja command, 
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.CreatedMatch(v => $"lojas/{v.Id}", v => new CriarLojaResponse(v.Id, v.Nome));
    }
}
