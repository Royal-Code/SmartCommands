using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.SmartProblems.HttpResults;

namespace RoyalCode.SmartCommands.Demo;

public static partial class MapProdutosApi
{
    public static RouteGroupBuilder MapProdutosGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("produtos");

        group.MapPost("/", CriarProduto2HandleAsync)
            .WithName("Criar Produto")
            .WithOpenApi();

        group.MapPut("/{id}", EditarProdutoHandleAsync)
            .WithName("Editar Produto")
            .WithOpenApi();

        group.MapGet("{id:guid}", FindProdutoHandleAsync)
            .WithName("Get product details")
            .WithDescription("Get product details by ID")
            .WithOpenApi();

        return group;
    }

    [ProduceProblems(ProblemCategory.InvalidParameter)]
    private static async Task<CreatedMatch<CriarProduto2Response>> CriarProduto2HandleAsync(
        ICriarProduto2Handler handler, 
        CriarProduto2 command, 
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.CreatedMatch(v => $"produtos/{v.Id}", v => new CriarProduto2Response(v.Id, v.Nome));
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

    [ProduceProblems(ProblemCategory.NotFound)]
    private static async Task<OkMatch<ProdutoDetalhes>> FindProdutoHandleAsync(
        Id<Produto, Guid> id, 
        IRepositoryAccessor<Produto> accessor, 
        CancellationToken ct)
    {
        var findResult = await accessor.FindEntityAsync<ProdutoDetalhes, Guid>(id, ct);
        if (findResult.NotFound(out var notfoundProblem))
            return notfoundProblem;

        return findResult.Entity;
    }
}
