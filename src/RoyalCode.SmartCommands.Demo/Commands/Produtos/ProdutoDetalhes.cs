using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartSelector;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

#nullable disable // poco

// AutoSelect<Produto> + AutoProperties gera a projecao (SelectProdutoExpression), o From(Produto) e as
// extensoes de consumo (IQueryable<Produto>/IEnumerable<Produto> -> ProdutoDetalhes) a partir dos nomes das
// propriedades de Produto, sem precisar declara-las nem escrever a expressao a mao (ver .docs/references/selector.md).
[MapGroup("produtos")]
[MapFind("{id:guid}", "Get product details"), EntityReference<Produto, Guid>]
[WithDescription("Get product details by ID")]
[AutoSelect<Produto>, AutoProperties]
public partial class ProdutoDetalhes
{
}
