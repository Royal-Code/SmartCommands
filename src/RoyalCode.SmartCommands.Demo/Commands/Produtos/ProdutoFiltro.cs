using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems.Conversions.Internals;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

[MapGroup("produtos")]
[MapSearch<Produto, ProblemDetails>("", "Listagem paginada de produtos")]
public class ProdutoFiltro
{
    public string? Nome { get; set; }

    public bool? Ativo { get; set; }
}
