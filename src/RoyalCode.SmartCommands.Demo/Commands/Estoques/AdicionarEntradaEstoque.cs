using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Estoques;

[MapGroup("produtos")]
[MapPost("/{id:guid}/estoque/entradas", "adicionar-entrada-estoque")]
public partial class AdicionarEntradaEstoque
{
	public int Quantidade { get; set; }

	public bool HasProblems([NotNullWhen(true)] out Problems? problems)
	{
		return RuleSet.For<AdicionarEntradaEstoque>()
			.GreaterThan(Quantidade, 0)
			.HasProblems(out problems);
	}

	// WithTransaction (DF21): o corpo altera estoque além do produto carregado; a transação exigida
	// pelo comando garante que cada tentativa do retry desfaça o trabalho parcial — e serve de
	// cenário real (SQLite + WorkContext) para transação exigida + retry.
	[Command, WithValidateModel, EditEntity<Produto, Guid>, WithWorkContext, WithTransaction, WithRetryOnConcurrency(Operation = "demo.estoques.adicionar")]
	internal async Task<Result> Execute(Produto produto, DemoDbContext db, CancellationToken ct)
	{
		var estoque = await db.Estoques.TryFindByAsync(e => e.ProdutoId == produto.Id, ct);
		if (estoque.NotFound(out var problem))
			return problem;

		return estoque.Entity.AdicionarEntrada(Quantidade);
	}
}
