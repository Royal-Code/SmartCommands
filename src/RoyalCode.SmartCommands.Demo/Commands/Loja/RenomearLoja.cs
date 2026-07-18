using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartCommands.Demo.Filters;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Commands.Lojas;

/// <summary>
/// Vitrine da Fase 10 (DF23): <c>WithResultStatus(NoContent)</c> descarta deliberadamente o valor de
/// sucesso (<c>Result&lt;Loja&gt;</c>) respondendo <c>204</c>, preservando os problemas (400/404);
/// os filtros de endpoint executam na ordem declarada (auditoria, depois carimbo, este ativado por DI);
/// as tags alimentam o OpenAPI.
/// </summary>
[MapGroup("lojas")]
[MapPut("/{id:int}/nome", "renomear-loja")]
[WithResultStatus(HttpResultStatus.NoContent)]
[WithTags("Lojas", "Administracao")]
[WithEndpointFilter<FiltroAuditoria>]
[WithEndpointFilter<FiltroCarimbo>]
[WithSummary("Renomear Loja")]
public partial class RenomearLoja
{
    public string? Nome { get; set; }

    [MemberNotNullWhen(false, nameof(Nome))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<RenomearLoja>()
            .NotEmpty(Nome)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, EditEntity<Loja, int>, WithUnitOfWork<IWorkContext>]
    internal Result<Loja> Execute(Loja loja)
    {
        WasValidated();

        loja.Renomear(Nome);
        return loja;
    }
}
