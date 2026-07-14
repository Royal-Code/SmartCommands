using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartSelector;

namespace RoyalCode.SmartCommands.Demo.Commands.Pedidos;

#nullable disable // POCO

// Usado como DTO de listagem em SearchReference<Pedido, PedidoResumo> (PedidoFiltro). AutoSelect<Pedido> +
// AutoProperties gera a projecao inteira por convencao de nome, sem propriedades declaradas.
[AutoSelect<Pedido>, AutoProperties]
public partial class PedidoResumo
{
}
