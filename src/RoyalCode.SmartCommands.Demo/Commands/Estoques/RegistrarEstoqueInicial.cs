using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Estoques;

[MapGroup("produtos")]
[MapPost("/{id:guid}/estoque", "registrar-estoque-inicial")]
public partial class RegistrarEstoqueInicial
{
	public int Quantidade { get; set; }

	public bool HasProblems([NotNullWhen(true)] out Problems? problems)
	{
		return RuleSet.For<RegistrarEstoqueInicial>()
			.GreaterThan(Quantidade, 0)
			.HasProblems(out problems);
	}

	[Command, WithValidateModel, EditEntity<Produto, Guid>, WithWorkContext, WithRetryOnConcurrency(Operation = "demo.estoques.registrar")]
	internal async Task<Result> Execute(Produto produto, DemoDbContext db, CancellationToken ct)
	{
		if (await db.Estoques.AnyAsync(e => e.ProdutoId == produto.Id, ct))
			return Problems.InvalidState(
				"O estoque inicial ja foi registrado para este produto.",
				typeId: "demo.estoque.ja_registrado");

		return await Task.FromResult(ProdutoEstoque.RegistrarInicial(produto, Quantidade))
			.ContinueAsync(db, static (estoque, db) =>
			{
				db.Estoques.Add(estoque);
				return Result.Ok();
			});
	}
}
