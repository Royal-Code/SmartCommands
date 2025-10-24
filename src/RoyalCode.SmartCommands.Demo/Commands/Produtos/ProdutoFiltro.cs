using Microsoft.AspNetCore.Mvc;
using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartSearch;
using RoyalCode.SmartSearch.AspNetCore.Internals;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

[MapGroup("produtos")]
[MapSearch("", "Listagem paginada de produtos")]
[SearchReference<Produto, ProdutoDetalhes>]
public class ProdutoFiltro
{
    public string? Nome { get; set; }

    public bool? Ativo { get; set; }
}


[MapGroup("produtos")]
[MapSearch("/{id:int}", "Listagem paginada de produtos exemplos")]
[SearchReference<Produto, ProdutoDetalhes>]
public class ExemploProdutoFiltro
{
    public string? Nome { get; set; }

    public bool? Ativo { get; set; }

    [WithFilter]
    internal void ConfigureSearch(ICriteria<Produto> search, HttpContext context, SomeService some, [WithParameter] int id)
    {
        var user = context.User.Identity?.Name ?? "anonymous";

        // aplica outros filtros ...
        //search.FilterBy()
    }

    internal Task AnotherMethod()
    {
        // método normal da classe de filtro
        return Task.CompletedTask;
    }
}

public class SomeService
{
    // apenas para exemplo de injeção de dependência
}
