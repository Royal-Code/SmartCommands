using RoyalCode.Entities;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Domain;

public class Pedido : Entity<Guid>
{
	private Pedido(IEnumerable<NovoPedidoItem> itens)
	{
		CriadoEm = DateTimeOffset.UtcNow;
		Status = PedidoStatus.Aberto;

		foreach (var item in itens)
			Itens.Add(new PedidoItem(item));

		Total = Itens.Sum(i => i.Total);
	}

#nullable disable
	protected Pedido() { }
#nullable enable

	public PedidoStatus Status { get; private set; }

	public decimal Total { get; private set; }

	public DateTimeOffset CriadoEm { get; private set; }

	public List<PedidoItem> Itens { get; private set; } = [];

	public static Result<Pedido> Criar(IReadOnlyCollection<NovoPedidoItem> itens)
	{
		if (itens.Count == 0)
			return Problems.InvalidState(
				"O pedido deve possuir ao menos um item.",
				typeId: "demo.pedido.sem_itens");

		if (itens.Any(i => i.Quantidade <= 0))
			return Problems.InvalidState(
				"Todos os itens do pedido devem possuir quantidade maior que zero.",
				typeId: "demo.pedido.quantidade_invalida");

		return new Pedido(itens);
	}

	public Result Cancelar()
	{
		if (Status == PedidoStatus.Cancelado)
			return Problems.InvalidState(
				"O pedido ja esta cancelado.",
				typeId: "demo.pedido.ja_cancelado");

		Status = PedidoStatus.Cancelado;
		return Result.Ok();
	}
}
