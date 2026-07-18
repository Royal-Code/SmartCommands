using Microsoft.AspNetCore.Mvc;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartCommands.Demo.Filters;
using RoyalCode.SmartSearch;
using RoyalCode.SmartSearch.AspNetCore.Internals;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

[MapGroup("produtos")]
[MapSearch("", "Listagem paginada de produtos")]
[SearchReference<Produto, ProdutoDetalhes>]
[WithEndpointFilter<FiltroAuditoria>]
public class ProdutoFiltro
{
    // string -> operador Like por convencao (substring, sem curinga informado pelo usuario). Case = Insensitive
    // normaliza os dois lados com ToUpper() na emissao portavel, entao a busca por nome nao depende de o
    // cliente acertar a caixa (ver .docs/references/smartsearch.md, secao 6.2).
    [Criterion(Case = CriterionCase.Insensitive)]
    public string? Nome { get; set; }

    [Criterion(Case = CriterionCase.Insensitive)]
    public string? Sku { get; set; }

    // Busca livre por nome OU sku com um unico parametro de query (?nomeOuSku=...). O token "Or" no nome da
    // propriedade cria a disjuncao por convencao (mesmo valor comparado contra os dois caminhos), sem precisar
    // de [Disjunction] explicito (ver .docs/references/smartsearch.md, secao 7.3).
    [Criterion(TargetPropertyPath = "NomeOrSku", Case = CriterionCase.Insensitive)]
    public string? NomeOuSku { get; set; }

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
