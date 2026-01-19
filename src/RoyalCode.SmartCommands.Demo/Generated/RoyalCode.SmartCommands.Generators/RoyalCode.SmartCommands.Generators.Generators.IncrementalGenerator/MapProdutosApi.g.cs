using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.SmartProblems.HttpResults;
using RoyalCode.SmartSearch;
using RoyalCode.SmartSearch.AspNetCore.HttpResults;
using RoyalCode.SmartSearch.AspNetCore.Internals;

namespace RoyalCode.SmartCommands.Demo;

public static partial class MapProdutosApi
{
    public static RouteGroupBuilder MapProdutosGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("produtos");

        group.MapPost("/", CriarProduto2HandleAsync)
            .WithName("criar-produto")
            .WithDescription("Cria um novo produto com o nome informado.")
            .WithSummary("Criar Produto");

        group.MapPut("/{id}", EditarProdutoHandleAsync)
            .WithName("Editar Produto");

        group.MapGet("{id:guid}", FindProdutoHandleAsync)
            .WithName("Get product details")
            .WithDescription("Get product details by ID");

        group.MapGet("", SearchProdutoByProdutoFiltroAsync)
            .WithName("Listagem paginada de produtos");

        group.MapGet("/{id:int}", SearchProdutoByExemploProdutoFiltroAsync)
            .WithName("Listagem paginada de produtos exemplos");

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

    [ProduceProblems(ProblemCategory.InvalidParameter, ProblemCategory.InternalServerError)]
    private static Task<MatchSearch<ProdutoDetalhes>> SearchProdutoByProdutoFiltroAsync(
        [AsParameters]  ProdutoFiltro filter, 
        [AsParameters]  SearchOptions options, 
        [FromQuery]  Sorting[]? orderby, 
        [FromServices]  ICriteria<Produto> criteria, 
        [FromServices]  ILogger<ICriteria<Produto>> logger, 
        CancellationToken ct)
    {
        Action<ICriteria<Produto>>? configure = null;
        return Performer.SearchAsync<Produto, ProdutoDetalhes, ProdutoFiltro>(filter, options, orderby, criteria, configure, logger, ct);
    }

    [ProduceProblems(ProblemCategory.InvalidParameter, ProblemCategory.InternalServerError)]
    private static Task<MatchSearch<ProdutoDetalhes>> SearchProdutoByExemploProdutoFiltroAsync(
        [AsParameters]  ExemploProdutoFiltro filter, 
        [AsParameters]  SearchOptions options, 
        [FromQuery]  Sorting[]? orderby, 
        [FromServices]  ICriteria<Produto> criteria, 
        [FromServices]  ILogger<ICriteria<Produto>> logger, 
        HttpContext context, 
        [FromServices]  SomeService some, 
        [FromRoute]  int id, 
        CancellationToken ct)
    {
        Action<ICriteria<Produto>>? configure = (criteria) => filter.ConfigureSearch(criteria, context, some, id);
        return Performer.SearchAsync<Produto, ProdutoDetalhes, ExemploProdutoFiltro>(filter, options, orderby, criteria, configure, logger, ct);
    }
}
