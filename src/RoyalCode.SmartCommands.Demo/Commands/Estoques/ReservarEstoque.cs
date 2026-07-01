using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
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
		var estoque = await db.Estoques.SingleOrDefaultAsync(e => e.ProdutoId == produto.Id, ct);
		if (estoque is null)
			return Problems.InvalidState(
				"O estoque inicial ainda nao foi registrado para este produto.",
				typeId: "demo.estoque.nao_registrado");

		if (!estoque.TemDisponivelParaReservar(Quantidade))
			return Problems.InvalidState(
				$"Estoque insuficiente. Disponivel: {estoque.Disponivel}.",
				property: nameof(Quantidade),
				typeId: "demo.estoque.insuficiente");

		estoque.Reservar(Quantidade);

		return Result.Ok();
	}
}
