using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Lojas;

[MapGroup("lojas")]
[MapPost("/", "loja-criar")]
[MapResponseValues("Id", "Nome")]
[MapCreatedRoute("{0}", "Id")]
public partial class CriarLoja
{
    public string? Nome { get; set; }

    public string? Endereco { get; set; }

    [MemberNotNullWhen(false, nameof(Nome), nameof(Endereco))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        var result = RuleSet.For<CriarLoja>()
            .NotEmpty(Nome)
            .NotEmpty(Endereco)
            .HasProblems(out problems);

        return result;
    }

    [Command, ProduceNewEntity , WithValidateModel, WithUnitOfWork<IWorkContext>]
    internal Loja Execute()
    {
        WasValidated();

        var loja = new Loja(Nome, Endereco);
        return loja;
    }
}
