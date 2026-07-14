using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Estoques;

[MapGroup("produtos")]
[MapPost("/{id:guid}/estoque/reservas", "reservar-estoque")]
public partial class ReservarEstoque
{
	public int Quantidade { get; set; }

	public bool HasProblems([NotNullWhen(true)] out Problems? problems)
	{
		return RuleSet.For<ReservarEstoque>()
			.GreaterThan(Quantidade, 0)
			.HasProblems(out problems);
	}

	[Command, WithValidateModel, EditEntity<Produto, Guid>, WithWorkContext, WithRetryOnConcurrency(Operation = "demo.estoques.reservar")]
	internal async Task<Result> Execute(Produto produto, DemoDbContext db, CancellationToken ct)
	{
		var estoque = await db.Estoques.TryFindByAsync(e => e.ProdutoId == produto.Id, ct);
		if (estoque.NotFound(out var problem))
			return problem;

		return estoque.Entity.Reservar(Quantidade);
	}
}
