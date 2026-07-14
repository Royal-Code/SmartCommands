using RoyalCode.Entities;

namespace RoyalCode.SmartCommands.Demo.Domain;

public class PedidoItem : Entity<Guid>
{
	public PedidoItem(NovoPedidoItem item)
	{
		Id = Guid.NewGuid();
		ProdutoId = item.ProdutoId;
		ProdutoNome = item.ProdutoNome;
		ProdutoSku = item.ProdutoSku;
		Quantidade = item.Quantidade;
		PrecoUnitario = item.PrecoUnitario;
		Total = item.Quantidade * item.PrecoUnitario;
	}

#nullable disable
	protected PedidoItem() { }
#nullable enable

	public Guid PedidoId { get; private set; }

	public Guid ProdutoId { get; private set; }

	public string ProdutoNome { get; private set; }

	public string ProdutoSku { get; private set; }

	public int Quantidade { get; private set; }

	public decimal PrecoUnitario { get; private set; }

	public decimal Total { get; private set; }
}
