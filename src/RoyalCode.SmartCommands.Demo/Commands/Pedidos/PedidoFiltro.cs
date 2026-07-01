using RoyalCode.SmartCommands.Demo.Domain;

namespace RoyalCode.SmartCommands.Demo.Commands.Pedidos;

[MapGroup("pedidos")]
[MapSearch("", "Listagem paginada de pedidos")]
[SearchReference<Pedido, PedidoResumo>]
public class PedidoFiltro
{
	public PedidoStatus? Status { get; set; }
}
