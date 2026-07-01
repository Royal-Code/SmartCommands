using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

[MapGroup("produtos")]
[MapPut("/{id}", "Editar Produto")]
public partial class EditarProduto
{
    public string? Nome { get; set; }

    public decimal Preco { get; set; }

    [MemberNotNullWhen(false, nameof(Nome))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<EditarProduto>()
            .NotEmpty(Nome)
            .GreaterThan(Preco, 0m)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, EditEntity<Produto, Guid>, WithWorkContext, WithRetryOnConcurrency(Operation = "demo.produtos.editar")]
    internal void Execute(Produto produto)
    {
        WasValidated();

        // preserva o SKU (identidade do produto no catalogo); edita apenas nome e preco
        produto.Editar(Nome, Preco);
    }
}
