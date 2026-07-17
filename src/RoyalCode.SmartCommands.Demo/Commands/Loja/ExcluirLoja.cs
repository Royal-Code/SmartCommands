using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo.Commands.Lojas;

/// <summary>
/// DELETE da matriz Minimal API (Fase 9): comando sem corpo que responde <c>204 No Content</c>.
/// A exclusao e logica: a loja carregada por <c>EditEntity</c> e desativada e persistida pelo UoW.
/// </summary>
[MapGroup("lojas")]
[MapDelete("/{id:int}", "excluir-loja")]
[WithSummary("Excluir Loja")]
[WithDescription("Desativa a loja informada (exclusao logica).")]
public partial class ExcluirLoja
{
    [Command, EditEntity<Loja, int>, WithUnitOfWork<IWorkContext>]
    internal Result Execute(Loja loja)
    {
        loja.Desativar();
        return Result.Ok();
    }
}
