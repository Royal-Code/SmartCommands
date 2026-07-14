using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartSelector;

namespace RoyalCode.SmartCommands.Demo.Commands.Pedidos;

#nullable disable // POCO

// AutoSelect<Pedido> + AutoProperties gera Id/Status/Total/CriadoEm automaticamente por nome; Itens fica
// declarado manualmente porque e uma colecao de objetos complexos (PedidoItemDetalhes) - o generator projeta
// cada item estruturalmente (Select(...).ToList()), sem precisar de expressao escrita a mao
// (ver .docs/references/selector.md, secao "Colecao de objetos").
[MapGroup("pedidos")]
[MapFind("{id:guid}", "Get order details"), EntityReference<Pedido, Guid>]
[AutoSelect<Pedido>, AutoProperties]
public partial class PedidoDetalhes
{
	public List<PedidoItemDetalhes> Itens { get; set; }
}

public sealed class PedidoItemDetalhes
{
	public Guid ProdutoId { get; set; }

	public string ProdutoNome { get; set; }

	public string ProdutoSku { get; set; }

	public int Quantidade { get; set; }

	public decimal PrecoUnitario { get; set; }

	public decimal Total { get; set; }
}
