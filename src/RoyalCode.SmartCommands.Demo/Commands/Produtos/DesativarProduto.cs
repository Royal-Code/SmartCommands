using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

[MapGroup("produtos")]
[MapPatch("/{id}/desativar", "Desativar Produto")]
public partial class DesativarProduto
{
    // Sem Operation no atributo: ao esgotar o retry, o handler gerado NÃO consulta a
    // IConcurrencyRetryProblemFactory (nem as options ExhaustedProblem*), devolvendo o problema genérico.
    [Command, EditEntity<Produto, Guid>, WithWorkContext, WithRetryOnConcurrency]
    internal void Execute(Produto produto)
    {
        produto.Ativo = false;
    }
}
