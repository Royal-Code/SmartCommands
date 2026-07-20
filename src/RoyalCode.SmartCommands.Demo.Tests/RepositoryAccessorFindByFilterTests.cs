using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartCommands.Demo.Tests.Support;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo.Tests;

/// <summary>
/// Paridade do adapter WorkContext para a busca por chave alternativa/composta:
/// <see cref="IRepositoryAccessor{TEntity}"/> resolvido pelo DI da Demo delega ao
/// <c>IRepository&lt;TEntity&gt;.FindAsync&lt;TDto&gt;</c> do EnterprisePatterns, projetando no provider
/// e gerando o NotFound nomeando a entidade.
/// </summary>
public class RepositoryAccessorFindByFilterTests
{
    [Fact]
    public async Task Accessor_projeta_por_predicado_e_encontra()
    {
        // arrange
        using var factory = new DemoApiFactory();
        await factory.ResetDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var workContext = scope.ServiceProvider.GetRequiredService<IWorkContext>();
        var produto = new Produto("Produto Sku", "SKU-77", 10m);
        workContext.Add(produto);
        await workContext.SaveAsync();

        var accessor = scope.ServiceProvider.GetRequiredService<IRepositoryAccessor<Produto>>();

        // act
        var result = await accessor.FindEntityAsync<ProdutoDetalhes>(
            p => p.Sku == "SKU-77",
            [new FindCriterion(nameof(Produto.Sku), "SKU-77")],
            CancellationToken.None);

        // assert
        Assert.True(result.Found);
        Assert.Equal(produto.Id, result.Entity!.Id);
        Assert.Equal("Produto Sku", result.Entity.Nome);
    }

    [Fact]
    public async Task Accessor_notfound_nomeia_a_entidade_com_os_criterios()
    {
        // arrange
        using var factory = new DemoApiFactory();
        await factory.ResetDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IRepositoryAccessor<Produto>>();

        // act
        var result = await accessor.FindEntityAsync<ProdutoDetalhes>(
            p => p.Sku == "SKU-NOPE",
            [new FindCriterion(nameof(Produto.Sku), "SKU-NOPE")],
            CancellationToken.None);

        // assert
        Assert.True(result.NotFound(out var problem));
        Assert.Equal(ProblemCategory.NotFound, problem!.Category);
        Assert.Equal("The record of 'Produto' with Sku 'SKU-NOPE' was not found", problem.Detail);
        Assert.Equal(nameof(Produto), problem.Extensions!["entity"]);
        Assert.Equal("SKU-NOPE", problem.Extensions!["Sku"]);
    }

    [Fact]
    public async Task Accessor_propaga_cancelamento()
    {
        // arrange
        using var factory = new DemoApiFactory();
        await factory.ResetDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IRepositoryAccessor<Produto>>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act & assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            accessor.FindEntityAsync<ProdutoDetalhes>(
                p => p.Sku == "SKU-77",
                [new FindCriterion(nameof(Produto.Sku), "SKU-77")],
                cts.Token));
    }
}
