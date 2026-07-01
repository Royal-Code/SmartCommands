using System.Net;
using System.Net.Http.Json;
using RoyalCode.SmartCommands.Demo.Tests.Support;

namespace RoyalCode.SmartCommands.Demo.Tests;

// Fase 1 do plan-demo-usage-scenarios: catalogo com regras de dominio (SKU obrigatorio/unico, preco > 0,
// editar preservando SKU, listar/filtrar). Exercita SmartCommands + SmartValidations + SmartProblems +
// WorkContext + SmartSearch sobre o dominio proprio da demo (RoyalCode.SmartCommands.Demo.Domain).
public class CatalogoTests
{
    [Fact]
    public async Task CriarProduto_Valido_Retorna201_E_Persiste()
    {
        using var app = new DemoApiFactory();
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        var create = await client.PostAsJsonAsync("/produtos/", new { Nome = "Camiseta", Sku = "CAM-001", Preco = 49.90m });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadApiJsonAsync<ProdutoResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);

        var details = await GetDetailsAsync(client, created.Id);
        Assert.Equal("Camiseta", details.Nome);
        Assert.Equal("CAM-001", details.Sku);
        Assert.Equal(49.90m, details.Preco);
        Assert.True(details.Ativo);
    }

    [Fact]
    public async Task CriarProduto_ComNomeVazio_Retorna400()
    {
        using var app = new DemoApiFactory();
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        var response = await client.PostAsJsonAsync("/produtos/", new { Nome = "", Sku = "CAM-001", Preco = 49.90m });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CriarProduto_ComSkuVazio_Retorna400()
    {
        using var app = new DemoApiFactory();
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        var response = await client.PostAsJsonAsync("/produtos/", new { Nome = "Camiseta", Sku = "", Preco = 49.90m });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CriarProduto_ComPrecoInvalido_Retorna400(int preco)
    {
        using var app = new DemoApiFactory();
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        var response = await client.PostAsJsonAsync("/produtos/", new { Nome = "Camiseta", Sku = "CAM-001", Preco = preco });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CriarProduto_ComSkuDuplicado_RetornaConflito()
    {
        using var app = new DemoApiFactory();
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        var first = await client.PostAsJsonAsync("/produtos/", new { Nome = "Camiseta", Sku = "CAM-001", Preco = 49.90m });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await client.PostAsJsonAsync("/produtos/", new { Nome = "Outra", Sku = "CAM-001", Preco = 10m });

        await duplicate.AssertProblemAsync(HttpStatusCode.Conflict, "CAM-001");
    }

    [Fact]
    public async Task EditarProduto_AlteraNomeEPreco_E_PreservaSku()
    {
        using var app = new DemoApiFactory();
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        var create = await client.PostAsJsonAsync("/produtos/", new { Nome = "Camiseta", Sku = "CAM-001", Preco = 49.90m });
        var created = await create.Content.ReadApiJsonAsync<ProdutoResponse>();
        Assert.NotNull(created);

        var edit = await client.PutAsJsonAsync($"/produtos/{created.Id}", new { Nome = "Camiseta Premium", Preco = 79.90m });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var details = await GetDetailsAsync(client, created.Id);
        Assert.Equal("Camiseta Premium", details.Nome);
        Assert.Equal(79.90m, details.Preco);
        Assert.Equal("CAM-001", details.Sku); // SKU preservado (identidade do produto)
    }

    [Fact]
    public async Task ListarProdutos_FiltraPorSku_E_StatusAtivo()
    {
        using var app = new DemoApiFactory();
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        await CreateAsync(client, "Camiseta", "CAM-001", 49.90m);
        var bermudaId = await CreateAsync(client, "Bermuda", "BER-001", 39.90m);

        var desativar = await client.PatchAsync($"/produtos/{bermudaId}/desativar", content: null);
        Assert.Equal(HttpStatusCode.OK, desativar.StatusCode);

        // filtro por SKU retorna apenas o correspondente
        var bySku = await (await client.GetAsync("/produtos?sku=CAM-001")).Content.ReadApiTextAsync();
        Assert.Contains("CAM-001", bySku);
        Assert.DoesNotContain("BER-001", bySku);

        // filtro por status ativo nao retorna o produto desativado
        var ativos = await (await client.GetAsync("/produtos?ativo=true")).Content.ReadApiTextAsync();
        Assert.Contains("CAM-001", ativos);
        Assert.DoesNotContain("BER-001", ativos);
    }

    private static async Task<Guid> CreateAsync(HttpClient client, string nome, string sku, decimal preco)
    {
        var create = await client.PostAsJsonAsync("/produtos/", new { Nome = nome, Sku = sku, Preco = preco });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadApiJsonAsync<ProdutoResponse>();
        Assert.NotNull(created);
        return created.Id;
    }

    private static async Task<ProdutoDetails> GetDetailsAsync(HttpClient client, Guid id)
    {
        var find = await client.GetAsync($"/produtos/{id}");
        Assert.Equal(HttpStatusCode.OK, find.StatusCode);
        var details = await find.Content.ReadApiJsonAsync<ProdutoDetails>();
        Assert.NotNull(details);
        return details;
    }

    private sealed record ProdutoResponse(Guid Id, string Nome, string Sku);

    private sealed record ProdutoDetails(Guid Id, string Nome, string Sku, decimal Preco, bool Ativo);
}
