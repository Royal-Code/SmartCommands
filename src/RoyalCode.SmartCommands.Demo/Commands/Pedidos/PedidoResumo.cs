using RoyalCode.SmartCommands.Demo.Domain;
using System.Linq.Expressions;

namespace RoyalCode.SmartCommands.Demo.Commands.Pedidos;

#nullable disable // POCO

public partial class PedidoResumo
{
	public Guid Id { get; set; }

	public PedidoStatus Status { get; set; }

	public decimal Total { get; set; }

	public DateTimeOffset CriadoEm { get; set; }
}

public partial class PedidoResumo
{
	private static readonly Expression<Func<Pedido, PedidoResumo>> selectExpression = p => new PedidoResumo
	{
		Id = p.Id,
		Status = p.Status,
		Total = p.Total,
		CriadoEm = p.CriadoEm
	};

	private static readonly Func<Pedido, PedidoResumo> selectFunc = selectExpression.Compile();

	public static Expression<Func<Pedido, PedidoResumo>> SelectExpression => selectExpression;

	public static PedidoResumo From(Pedido pedido) => selectFunc(pedido);
}
