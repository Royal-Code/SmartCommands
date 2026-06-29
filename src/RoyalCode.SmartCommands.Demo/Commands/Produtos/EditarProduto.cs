using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

[MapGroup("produtos")]
[MapPut("/{id}", "Editar Produto")]
public partial class EditarProduto
{
    public string? Nome { get; set; }

    [MemberNotNullWhen(false, nameof(Nome))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        var result = RuleSet.For<CriarProduto2>()
            .NotEmpty(Nome)
            .HasProblems(out problems);

        return result;
    }

    [Command, WithValidateModel, EditEntity<Produto, Guid>, WithWorkContext, WithRetryOnConcurrency]
    internal void Execute(Produto produto)
    {
        WasValidated();

        produto.Nome = Nome;
    }
}
