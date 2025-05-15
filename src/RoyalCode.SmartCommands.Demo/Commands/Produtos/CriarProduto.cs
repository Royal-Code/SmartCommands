using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands;

[MapGroup("produtos")]
[MapPost("/", "Criar Produto")]
[MapResponseValues("Id", "Nome")]
[MapCreatedRoute("{0}", "Id")]
public partial class CriarProduto
{
    public string? Nome { get; set; }

    [MemberNotNullWhen(false, nameof(Nome))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        var result = RuleSet.For<CriarProduto>()
            .NotEmpty(Nome)
            .HasProblems(out problems);

        return result;
    }

    [Command, WithValidateModel, ProduceNewEntity, WithUnitOfWork<IWorkContext>]
    internal Produto ExecuteAsync()
    {
        WasValidated();

        return new Produto(Nome);
    }
}
