using RoyalCode.SmartCommands.Demo.Domain;
using System.Linq.Expressions;

namespace RoyalCode.SmartCommands.Demo.Commands.Pedidos;

#nullable disable // POCO

[MapGroup("pedidos")]
[MapFind("{id:guid}", "Get order details"), EntityReference<Pedido, Guid>]
public partial class PedidoDetalhes
{
	public Guid Id { get; set; }

	public PedidoStatus Status { get; set; }

	public decimal Total { get; set; }

	public DateTimeOffset CriadoEm { get; set; }

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

public partial class PedidoDetalhes
{
	private static readonly Expression<Func<Pedido, PedidoDetalhes>> selectExpression = p => new PedidoDetalhes
	{
		Id = p.Id,
		Status = p.Status,
		Total = p.Total,
		CriadoEm = p.CriadoEm,
		Itens = p.Itens
			.Select(i => new PedidoItemDetalhes
			{
				ProdutoId = i.ProdutoId,
				ProdutoNome = i.ProdutoNome,
				ProdutoSku = i.ProdutoSku,
				Quantidade = i.Quantidade,
				PrecoUnitario = i.PrecoUnitario,
				Total = i.Total
			})
			.ToList()
	};

	private static readonly Func<Pedido, PedidoDetalhes> selectFunc = selectExpression.Compile();

	public static Expression<Func<Pedido, PedidoDetalhes>> SelectExpression => selectExpression;

	public static PedidoDetalhes From(Pedido pedido) => selectFunc(pedido);
}
