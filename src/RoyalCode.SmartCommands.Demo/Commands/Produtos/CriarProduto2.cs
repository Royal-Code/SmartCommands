using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

[MapGroup("produtos")]
[MapPost("/", "Criar Produto")]
[MapResponseValues("Id", "Nome")]
[MapCreatedRoute("{0}", "Id")]
public partial class CriarProduto2
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

    [Command, WithValidateModel, ProduceNewEntity, WithUnitOfWork<IWorkContext>]
    internal Produto Execute()
    {
        WasValidated();

        return new Produto(Nome);
    }
}
