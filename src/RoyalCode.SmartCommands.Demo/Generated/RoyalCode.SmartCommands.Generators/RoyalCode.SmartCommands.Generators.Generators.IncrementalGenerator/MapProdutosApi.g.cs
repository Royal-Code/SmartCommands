using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.HttpResults;

namespace RoyalCode.SmartCommands.Demo;

public static partial class MapProdutosApi
{
    public static RouteGroupBuilder MapProdutosGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("produtos");

        group.MapPost("/", CriarProdutoHandleAsync)
            .WithName("Criar Produto")
            .WithOpenApi();

        group.MapPut("/{id}", EditarProdutoHandleAsync)
            .WithName("Editar Produto")
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

    [ProduceProblems(ProblemCategory.InvalidParameter)]
    private static async Task<OkMatch> EditarProdutoHandleAsync(
        IEditarProdutoHandler handler, 
        Guid produtoId, 
        EditarProduto command, 
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(produtoId, command, ct);
        return result;
    }
}
