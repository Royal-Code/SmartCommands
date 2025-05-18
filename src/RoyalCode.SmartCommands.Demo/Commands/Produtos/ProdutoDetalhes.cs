using RoyalCode.SmartCommands.Tests.Models;
using System.Collections;
using System.Linq.Expressions;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

#nullable disable // poco

[MapGroup("produtos")]
[MapFind<Produto>("{id:guid}", "Get product details")]
public partial class ProdutoDetalhes
{
    public Guid Id { get; set; }

    public string Nome { get; set; }

    public bool Ativo { get; set; }
}

public partial class ProdutoDetalhes
{
    private static readonly Expression<Func<Produto, ProdutoDetalhes>> selectExpression = p => new ProdutoDetalhes
    {
        Id = p.Id,
        Nome = p.Nome,
        Ativo = p.Ativo
    };

    private static readonly Func<Produto, ProdutoDetalhes> selectFunc = selectExpression.Compile();

    public static Expression<Func<Produto, ProdutoDetalhes>> SelectExpression => selectExpression;

    public static ProdutoDetalhes From(Produto produto) => selectFunc(produto);
}

public static class ProdutoDetalhes_Extensions
{
    public static IQueryable<ProdutoDetalhes> Select(this IQueryable<Produto> produtos)
    {
        return produtos.Select(ProdutoDetalhes.SelectExpression);
    }

    public static IEnumerable<ProdutoDetalhes> Select(this IEnumerable<Produto> produtos)
    {
        return produtos.Select(ProdutoDetalhes.From);
    }
}