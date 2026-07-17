using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Pedidos;

[MapGroup("pedidos")]
[MapPost("/", "criar-pedido")]
[MapResponseValues(nameof(Pedido.Id), nameof(Pedido.Status), nameof(Pedido.Total))]
[MapCreatedRoute("{id}", nameof(Pedido.Id))]
public partial class CriarPedido
{
	public List<CriarPedidoItem>? Itens { get; set; }

	[MemberNotNullWhen(false, nameof(Itens))]
	public bool HasProblems([NotNullWhen(true)] out Problems? problems)
	{
		// Colecao obrigatoria com itens obrigatoriamente validos: NotEmpty cobre nulo/vazio; When + NotNullNested
		// valida cada item so quando a colecao existe, evitando reportar o mesmo problema duas vezes (ver
		// validations.ai-rules.md, secao "Nested Collections"). Caminhos de erro ficam indexados por item,
		// ex.: "Itens[0].ProdutoId".
		return RuleSet.For<CriarPedido>()
			.NotEmpty((ICollection<CriarPedidoItem>?)Itens)
			.When(Itens is not null, s => s.NotNullNested(Itens, item =>
				RuleSet.For<CriarPedidoItem>()
					.WithPropertyPrefix(nameof(item))
					.NotEmpty(item.ProdutoId)
					.GreaterThan(item.Quantidade, 0)))
			.HasProblems(out problems);
	}

	// Reserva estoque (muta ProdutoEstoque, token Version) e cria o pedido na mesma unidade de trabalho, sob retry de
	// concorrencia. Usa o overload generico RetryOnConcurrencyAsync<Pedido> (ProduceNewEntity + Result<Pedido>): dois
	// pedidos concorrentes no mesmo produto reexecutam o corpo (recarregando estoque) em vez de vazar 500.
	[Command, WithValidateModel, ProduceNewEntity, WithWorkContext, WithRetryOnConcurrency(Operation = "demo.pedidos.criar")]
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
