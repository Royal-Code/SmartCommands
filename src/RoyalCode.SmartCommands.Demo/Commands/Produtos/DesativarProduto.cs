using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

[MapGroup("produtos")]
[MapPatch("/{id}/desativar", "Desativar Produto")]
public partial class DesativarProduto
{
    // Sem Operation no atributo: ao esgotar o retry, o handler gerado NAO consulta a
    // IConcurrencyRetryProblemFactory (nem as options ExhaustedProblem*), devolvendo o problema generico.
    // A transicao devolve Result: desativar um produto ja inativo e 409 (regra no agregado).
    [Command, EditEntity<Produto, Guid>, WithWorkContext, WithRetryOnConcurrency]
    internal Result Execute(Produto produto) => produto.Desativar();
}
