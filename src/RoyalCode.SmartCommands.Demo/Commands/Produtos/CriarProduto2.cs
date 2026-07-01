using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

[MapGroup("produtos")]
[MapPost("/", "criar-produto")]
[MapResponseValues("Id", "Nome", "Sku")]
[MapCreatedRoute("{0}", "Id")]
[WithDescription("Cria um novo produto no catalogo.")]
[WithSummary("Criar Produto")]
public partial class CriarProduto2
{
    public string? Nome { get; set; }

    public string? Sku { get; set; }

    public decimal Preco { get; set; }

    [MemberNotNullWhen(false, nameof(Nome), nameof(Sku))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CriarProduto2>()
            .NotEmpty(Nome)
            .NotEmpty(Sku)
            .GreaterThan(Preco, 0m)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, ProduceNewEntity, WithUnitOfWork<IWorkContext>]
    internal async Task<Result<Produto>> Execute(DemoDbContext db, CancellationToken ct)
    {
        WasValidated();

        // unicidade de SKU: valida antes de gravar e devolve um conflito amigavel (409)
        if (await db.Produtos.AnyAsync(p => p.Sku == Sku, ct))
            return Problems.InvalidState(
                $"Ja existe um produto com o SKU '{Sku}'.",
                property: nameof(Sku),
                typeId: "demo.produto.sku_duplicado");

        return new Produto(Nome, Sku, Preco);
    }
}
