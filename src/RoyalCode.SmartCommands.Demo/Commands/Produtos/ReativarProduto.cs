using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

[MapGroup("produtos")]
[MapPatch("/{id}/reativar", "Reativar Produto")]
public partial class ReativarProduto
{
    // Comando bodyless (sem props): instanciado via new no endpoint. A transicao devolve Result:
    // reativar um produto ja ativo e 409 (regra no agregado).
    [Command, EditEntity<Produto, Guid>, WithWorkContext, WithRetryOnConcurrency]
    internal Result Execute(Produto produto) => produto.Reativar();
}
