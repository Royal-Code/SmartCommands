using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Lojas;

/// <summary>
/// Vitrine da Fase 10 (DF23): <c>WithResultStatus(Created)</c> sem <c>MapCreatedRoute</c> responde
/// <c>201</c> sem header <c>Location</c>, com o corpo projetado por <c>MapResponseValues</c>.
/// </summary>
[MapGroup("lojas")]
[MapPost("/importadas", "importar-loja")]
[WithResultStatus(HttpResultStatus.Created)]
[MapResponseValues(nameof(Loja.Id), nameof(Loja.Nome))]
[WithTags("Lojas")]
[WithSummary("Importar Loja")]
public partial class ImportarLoja
{
    public string? Nome { get; set; }

    public string? Endereco { get; set; }

    [MemberNotNullWhen(false, nameof(Nome), nameof(Endereco))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<ImportarLoja>()
            .NotEmpty(Nome)
            .NotEmpty(Endereco)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, ProduceNewEntity, WithUnitOfWork<IWorkContext>]
    internal Loja Execute()
    {
        WasValidated();

        return new Loja(Nome, Endereco);
    }
}
