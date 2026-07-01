using RoyalCode.SmartCommands.Demo.Domain;
using System.Linq.Expressions;

namespace RoyalCode.SmartCommands.Demo.Commands.Estoques;

public partial class ProdutoEstoqueDetalhes
{
    private static Func<ProdutoEstoque, ProdutoEstoqueDetalhes> selectProdutoEstoqueFunc;

    public static Expression<Func<ProdutoEstoque, ProdutoEstoqueDetalhes>> SelectProdutoEstoqueExpression { get; } = a => new ProdutoEstoqueDetalhes
    {
        ProdutoId = a.ProdutoId,
        Disponivel = a.Disponivel,
        Reservado = a.Reservado,
        Version = a.Version
    };

    public static ProdutoEstoqueDetalhes From(ProdutoEstoque produtoEstoque) => (selectProdutoEstoqueFunc ??= SelectProdutoEstoqueExpression.Compile())(produtoEstoque);
}
