using RoyalCode.SmartCommands.Tests.Models;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

[MapGroup("produtos")]
[MapSearch("", "Listagem paginada de produtos")]
[SearchReference<Produto, ProdutoDetalhes>]
public class ProdutoFiltro
{
    public string? Nome { get; set; }

    public bool? Ativo { get; set; }
}
