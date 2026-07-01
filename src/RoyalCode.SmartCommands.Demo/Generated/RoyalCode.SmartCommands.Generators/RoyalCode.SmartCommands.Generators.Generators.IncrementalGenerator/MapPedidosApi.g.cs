using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Pedidos;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.SmartProblems.HttpResults;
using RoyalCode.SmartSearch;
using RoyalCode.SmartSearch.AspNetCore.HttpResults;
using RoyalCode.SmartSearch.AspNetCore.Internals;

namespace RoyalCode.SmartCommands.Demo;

public static partial class MapPedidosApi
{
    public static RouteGroupBuilder MapPedidosGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("pedidos");

        group.MapPatch("/{id:guid}/cancelar", CancelarPedidoHandleAsync)
            .WithName("cancelar-pedido");

        group.MapPost("/", CriarPedidoHandleAsync)
            .WithName("criar-pedido");

        group.MapGet("{id:guid}", FindPedidoHandleAsync)
            .WithName("Get order details");

        group.MapGet("", SearchPedidoByPedidoFiltroAsync)
            .WithName("Listagem paginada de pedidos");

        return group;
    }

    private static async Task<OkMatch> CancelarPedidoHandleAsync(
        ICancelarPedidoHandler handler, 
        [FromRoute(Name = "id")]  Guid pedidoId, 
        CancellationToken ct)
    {
        var command = new CancelarPedido();

        var result = await handler.HandleAsync(pedidoId, command, ct);
        return result;
    }

    [ProduceProblems(ProblemCategory.InvalidParameter)]
    private static async Task<CreatedMatch<CriarPedidoResponse>> CriarPedidoHandleAsync(
        ICriarPedidoHandler handler, 
        CriarPedido command, 
        CancellationToken ct)
    {
        if (command is null)
            return Problems.InvalidParameter("The request body is required.");

        var result = await handler.HandleAsync(command, ct);
        return result.CreatedMatch(v => $"pedidos/{v.Id}", v => new CriarPedidoResponse(v.Id, v.Status, v.Total));
    }

    [ProduceProblems(ProblemCategory.NotFound)]
    private static async Task<OkMatch<PedidoDetalhes>> FindPedidoHandleAsync(
        Id<Pedido, Guid> id, 
        IRepositoryAccessor<Pedido> accessor, 
        CancellationToken ct)
    {
        var findResult = await accessor.FindEntityAsync<PedidoDetalhes, Guid>(id, ct);
        if (findResult.NotFound(out var notfoundProblem))
            return notfoundProblem;

        return findResult.Entity;
    }

    [ProduceProblems(ProblemCategory.InvalidParameter, ProblemCategory.InternalServerError)]
    private static Task<MatchSearch<PedidoResumo>> SearchPedidoByPedidoFiltroAsync(
        [AsParameters]  PedidoFiltro filter, 
        [AsParameters]  SearchOptions options, 
        [FromQuery]  Sorting[]? orderby, 
        [FromServices]  ICriteria<Pedido> criteria, 
        [FromServices]  ILogger<ICriteria<Pedido>> logger, 
        CancellationToken ct)
    {
        Action<ICriteria<Pedido>>? configure = null;
        return Performer.SearchAsync<Pedido, PedidoResumo, PedidoFiltro>(filter, options, orderby, criteria, configure, logger, ct);
    }
}
