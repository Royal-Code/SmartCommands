using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Pedidos;

[MapGroup("pedidos")]
[MapPost("/", "criar-pedido")]
[MapResponseValues("Id", "Status", "Total")]
[MapCreatedRoute("{0}", "Id")]
public partial class CriarPedido
{
	public List<CriarPedidoItem>? Itens { get; set; }

	[MemberNotNullWhen(false, nameof(Itens))]
	public bool HasProblems([NotNullWhen(true)] out Problems? problems)
	{
		if (Itens is null)
		{
			problems = Problems.InvalidParameter(
				"O pedido deve possuir ao menos um item.",
				property: nameof(Itens),
				typeId: "demo.pedido.sem_itens");
			return true;
		}

		if (RuleSet.For<CriarPedido>()
			.NotEmpty((ICollection<CriarPedidoItem>)Itens)
			.HasProblems(out problems))
			return true;

		if (Itens.Any(i => i.ProdutoId == Guid.Empty || i.Quantidade <= 0))
		{
			problems = Problems.InvalidParameter(
				"Todos os itens devem possuir produto e quantidade maior que zero.",
				property: nameof(Itens),
				typeId: "demo.pedido.item_invalido");
			return true;
		}

		problems = null;
		return false;
	}

	// GAP conhecido (ver plan-demo-usage-scenarios "Registro de gaps"): este comando muta ProdutoEstoque (token
	// Version) ao reservar, mas nao tem [WithRetryOnConcurrency] porque ProduceNewEntity/Result<Pedido> nao e coberto
	// pela primitiva de retry atual (so a forma sem valor). Consequencia: dois pedidos concorrentes no mesmo produto
	// podem gerar ConcurrencyException cru -> 500. Retomar quando o overload generico RetryOnConcurrencyAsync<T> existir.
	[Command, WithValidateModel, ProduceNewEntity, WithUnitOfWork<IWorkContext>]
	internal async Task<Result<Pedido>> Execute(DemoDbContext db, CancellationToken ct)
	{
		WasValidated();

		var itensNormalizados = Itens
			.GroupBy(i => i.ProdutoId)
			.Select(g => new CriarPedidoItem
			{
				ProdutoId = g.Key,
				Quantidade = g.Sum(i => i.Quantidade)
			})
			.ToArray();

		var produtosIds = itensNormalizados.Select(i => i.ProdutoId).ToArray();
		var produtos = await db.Produtos
			.Where(p => produtosIds.Contains(p.Id))
			.ToDictionaryAsync(p => p.Id, ct);
		var estoques = await db.Estoques
			.Where(e => produtosIds.Contains(e.ProdutoId))
			.ToDictionaryAsync(e => e.ProdutoId, ct);

		List<NovoPedidoItem> itensPedido = [];

		foreach (var item in itensNormalizados)
		{
			if (!produtos.TryGetValue(item.ProdutoId, out var produto))
				return Problems.InvalidParameter(
					$"Produto '{item.ProdutoId}' nao encontrado.",
					property: nameof(Itens),
					typeId: "demo.pedido.produto_nao_encontrado");

			if (!produto.Ativo)
				return Problems.InvalidState(
					$"Produto '{produto.Sku}' esta inativo.",
					typeId: "demo.pedido.produto_inativo");

			if (!estoques.TryGetValue(item.ProdutoId, out var estoque))
				return Problems.InvalidState(
					$"Estoque inicial do produto '{produto.Sku}' ainda nao foi registrado.",
					typeId: "demo.pedido.estoque_nao_registrado");

			var reserva = estoque.Reservar(item.Quantidade);
			if (reserva.HasProblems(out var reservaProblems))
				return reservaProblems;

			itensPedido.Add(new NovoPedidoItem(
				produto.Id,
				produto.Nome,
				produto.Sku,
				item.Quantidade,
				produto.Preco));
		}

		return Pedido.Criar(itensPedido);
	}
}

public sealed class CriarPedidoItem
{
	public Guid ProdutoId { get; set; }

	public int Quantidade { get; set; }
}
