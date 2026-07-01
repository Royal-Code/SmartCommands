using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Pedidos;

[MapGroup("pedidos")]
[MapPatch("/{id:guid}/cancelar", "cancelar-pedido")]
public partial class CancelarPedido
{
	[Command, EditEntity<Pedido, Guid>, WithWorkContext, WithRetryOnConcurrency(Operation = "demo.pedidos.cancelar")]
	internal async Task<Result> Execute(Pedido pedido, DemoDbContext db, CancellationToken ct)
	{
		await db.Entry(pedido)
			.Collection(p => p.Itens)
			.LoadAsync(ct);

		var cancelar = pedido.Cancelar();
		if (!cancelar.IsSuccess)
			return cancelar;

		var produtosIds = pedido.Itens.Select(i => i.ProdutoId).ToArray();
		var estoques = await db.Estoques
			.Where(e => produtosIds.Contains(e.ProdutoId))
			.ToDictionaryAsync(e => e.ProdutoId, ct);

		foreach (var item in pedido.Itens)
		{
			if (!estoques.TryGetValue(item.ProdutoId, out var estoque))
				return Problems.InvalidState(
					$"Estoque do produto '{item.ProdutoSku}' nao foi encontrado.",
					typeId: "demo.pedido.estoque_nao_encontrado");

			var liberacao = estoque.LiberarReserva(item.Quantidade);
			if (!liberacao.IsSuccess)
				return liberacao;
		}

		return Result.Ok();
	}
}
