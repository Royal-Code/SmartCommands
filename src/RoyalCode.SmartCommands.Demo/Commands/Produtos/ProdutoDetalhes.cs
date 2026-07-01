using RoyalCode.SmartCommands.Demo.Domain;
using System.Linq.Expressions;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

#nullable disable // poco

[MapGroup("produtos")]
[MapFind("{id:guid}", "Get product details"), EntityReference<Produto, Guid>]
[WithDescription("Get product details by ID")]
public partial class ProdutoDetalhes
{
    public Guid Id { get; set; }

    public string Nome { get; set; }

    public string Sku { get; set; }

    public decimal Preco { get; set; }

    public bool Ativo { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
}

public partial class ProdutoDetalhes
{
    private static readonly Expression<Func<Produto, ProdutoDetalhes>> selectExpression = p => new ProdutoDetalhes
    {
        Id = p.Id,
        Nome = p.Nome,
        Sku = p.Sku,
        Preco = p.Preco,
        Ativo = p.Ativo,
        CriadoEm = p.CriadoEm
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
