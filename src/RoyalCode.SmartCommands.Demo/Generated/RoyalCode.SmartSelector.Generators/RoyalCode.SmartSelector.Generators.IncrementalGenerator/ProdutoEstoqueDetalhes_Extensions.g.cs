using RoyalCode.SmartCommands.Demo.Domain;

namespace RoyalCode.SmartCommands.Demo.Commands.Estoques;

public static class ProdutoEstoqueDetalhes_Extensions
{
    public static IQueryable<ProdutoEstoqueDetalhes> SelectProdutoEstoqueDetalhes(this IQueryable<ProdutoEstoque> query)
    {
        return query.Select(ProdutoEstoqueDetalhes.SelectProdutoEstoqueExpression);
    }

    public static IEnumerable<ProdutoEstoqueDetalhes> SelectProdutoEstoqueDetalhes(this IEnumerable<ProdutoEstoque> enumerable)
    {
        return enumerable.Select(ProdutoEstoqueDetalhes.From);
    }

    public static ProdutoEstoqueDetalhes ToProdutoEstoqueDetalhes(this ProdutoEstoque produtoEstoque) => ProdutoEstoqueDetalhes.From(produtoEstoque);
}
