using Microsoft.AspNetCore.Mvc;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartSearch;
using RoyalCode.SmartSearch.AspNetCore.Internals;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

[MapGroup("produtos")]
[MapSearch("", "Listagem paginada de produtos")]
[SearchReference<Produto, ProdutoDetalhes>]
public class ProdutoFiltro
{
    // string -> operador Like (Contains) por convencao: busca por nome parcial.
    public string? Nome { get; set; }

    public string? Sku { get; set; }

    public bool? Ativo { get; set; }

    // Faixa de preco: dois criterios sobre o mesmo alvo Produto.Preco (>= e <=). Nulos sao ignorados
    // (IgnoreIfIsEmpty padrao), entao a faixa pode ser aberta em qualquer ponta.
    [Criterion(nameof(Produto.Preco), CriterionOperator.GreaterThanOrEqual)]
    public decimal? PrecoMinimo { get; set; }

    [Criterion(nameof(Produto.Preco), CriterionOperator.LessThanOrEqual)]
    public decimal? PrecoMaximo { get; set; }

    // Disponibilidade em estoque via caminho aninhado na navegacao de leitura Produto.Estoque.
    // "Disponivel" ja desconta o reservado, entao um produto totalmente reservado (Disponivel == 0) ou
    // sem estoque registrado (Estoque == null -> LEFT JOIN nulo) nao satisfaz o criterio.
    [Criterion("Estoque.Disponivel", CriterionOperator.GreaterThanOrEqual)]
    public int? EstoqueDisponivelMinimo { get; set; }

    // Flag administrativa (Fase 5): quando true, a busca inclui produtos inativos. Nao e um criterio direto
    // sobre Produto (Ignore), apenas alimenta a regra de visibilidade abaixo.
    [Criterion(Ignore = true)]
    public bool? IncluirInativos { get; set; }

    // Visibilidade padrao (Fase 5): a listagem publica esconde inativos. Quando o chamador nao pediu
    // IncluirInativos e nao filtrou Ativo explicitamente, forcamos Ativo = true (reaproveitando o criterio
    // Ativo ja existente). Filtros sao lidos de forma lazy na execucao (FilterBy so guarda o objeto), entao
    // esta mutacao no [WithFilter] e honrada. Admin usa ?incluirInativos=true; quem quer so inativos usa ?ativo=false.
    [WithFilter]
    internal void AplicarVisibilidade(ICriteria<Produto> criteria)
    {
        if (IncluirInativos != true && Ativo is null)
            Ativo = true;
    }
}


[MapGroup("produtos")]
[MapSearch("/filtro/{id:int}", "Listagem paginada de produtos exemplos")]
[SearchReference<Produto, ProdutoDetalhes>]
public class ExemploProdutoFiltro
{
    public string? Nome { get; set; }

    public string? Sku { get; set; }

    public bool? Ativo { get; set; }

    [WithFilter]
    internal void ConfigureSearch(
        ICriteria<Produto> search,
        HttpContext context,
        SomeService some,
        [WithParameter] int id)
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
