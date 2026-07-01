using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Pedidos;

public interface ICriarPedidoHandler
{
    public Task<Result<Pedido>> HandleAsync(CriarPedido command, CancellationToken ct);
}
