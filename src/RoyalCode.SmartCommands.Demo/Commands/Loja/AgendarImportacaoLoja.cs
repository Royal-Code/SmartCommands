using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Lojas;

/// <summary>
/// Vitrine da Fase 3 (DF17): <c>MapAcceptedRoute</c> responde <c>202 Accepted</c> com header
/// <c>Location</c> montado a partir do valor de sucesso e o corpo projetado por <c>MapResponseValues</c>.
/// O <c>Location</c> aponta para o recurso que permite acompanhar o processamento aceito.
/// </summary>
[MapGroup("lojas")]
[MapPost("/agendamentos", "agendar-importacao-loja")]
[MapAcceptedRoute("agendamentos/{id}/{nome}/status", nameof(Loja.Id), nameof(Loja.Nome))]
[MapResponseValues(nameof(Loja.Id), nameof(Loja.Nome))]
[WithTags("Lojas")]
[WithSummary("Agendar Importação de Loja")]
public partial class AgendarImportacaoLoja
{
    public string? Nome { get; set; }

    public string? Endereco { get; set; }

    [MemberNotNullWhen(false, nameof(Nome), nameof(Endereco))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<AgendarImportacaoLoja>()
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
