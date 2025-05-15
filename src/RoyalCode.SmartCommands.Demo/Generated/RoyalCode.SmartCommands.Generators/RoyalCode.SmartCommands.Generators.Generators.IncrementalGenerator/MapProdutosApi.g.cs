using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartCommands.Demo.Commands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.HttpResults;

namespace RoyalCode.SmartCommands.Demo.Apis;

public static partial class MapProdutosApi
{
    public static RouteGroupBuilder MapProdutosGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("produtos");

        group.MapPost("/", CriarProdutoHandleAsync)
            .WithName("Criar Produto")
            .WithOpenApi();

        return group;
    }

    [ProduceProblems(ProblemCategory.InvalidParameter)]
    private static async Task<CreatedMatch<CriarProdutoResponse>> CriarProdutoHandleAsync(
        ICriarProdutoHandler handler, 
        CriarProduto command, 
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.CreatedMatch(v => $"produtos/{v.Id}", v => new CriarProdutoResponse(v.Id, v.Nome));
    }
}
