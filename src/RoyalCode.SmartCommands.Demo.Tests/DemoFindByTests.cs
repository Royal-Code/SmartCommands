using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RoyalCode.SmartCommands.Demo.Tests.Support;

namespace RoyalCode.SmartCommands.Demo.Tests;

/// <summary>
/// Fase 5 (DF10): <c>MapFindBy</c> — busca por chave alternativa (Produto por SKU) e composta
/// (PedidoItem por PedidoId + ProdutoSku), projetando no DTO. Verifica sucesso, <c>404</c> rico que nomeia a
/// entidade e o OpenAPI (parâmetros de rota e respostas).
/// </summary>
public class DemoFindByTests
{
    [Fact]
    public async Task Chave_simples_encontra_produto_por_sku_e_projeta_o_dto()
    {
        using var app = new DemoApiFactory();
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        var produtoId = await CreateProductAsync(client, "Camiseta FindBy", "FB-SKU-001", 25m);

        var response = await client.GetAsync("/produtos/por-sku/FB-SKU-001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadApiJsonAsync<ProdutoPorSkuResponse>();
        Assert.NotNull(dto);
        Assert.Equal(produtoId, dto.Id);
        Assert.Equal("FB-SKU-001", dto.Sku);
        Assert.Equal("Camiseta FindBy", dto.Nome);
    }

    [Fact]
    public async Task Chave_simples_ausente_responde_404_nomeando_a_entidade()
    {
        using var app = new DemoApiFactory();
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        var response = await client.GetAsync("/produtos/por-sku/NAO-EXISTE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;
        // o problema NotFound nomeia a entidade (Produto), não o DTO (DF15), e cita o critério Sku
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("Produto", root.GetProperty("entity").GetString());
        Assert.Equal("NAO-EXISTE", root.GetProperty("Sku").GetString());
    }

    [Fact]
    public async Task Chave_composta_encontra_o_item_do_pedido_por_pedidoId_e_sku()
    {
        using var app = new DemoApiFactory();
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        var produtoId = await CreateProductAsync(client, "Camiseta Comp", "COMP-001", 25m);
        await RegistrarEstoqueInicialAsync(client, produtoId, 10);
        var pedidoId = await CreatePedidoAsync(client, produtoId, 2);

        var response = await client.GetAsync($"/pedidos/{pedidoId}/itens/COMP-001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadApiJsonAsync<ItemPedidoPorSkuResponse>();
        Assert.NotNull(dto);
        Assert.Equal(pedidoId, dto.PedidoId);
        Assert.Equal("COMP-001", dto.ProdutoSku);
        Assert.Equal(2, dto.Quantidade);
    }

    [Fact]
    public async Task Chave_composta_ausente_responde_404_com_ProblemDetails_estruturado()
    {
        using var app = new DemoApiFactory();
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        var pedidoId = Guid.NewGuid();
        var response = await client.GetAsync($"/pedidos/{pedidoId}/itens/NAO-EXISTE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;

        // status e nomeação da entidade (DF15: nomeia a entidade, não o DTO)
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("PedidoItem", root.GetProperty("entity").GetString());

        // os critérios entram como extension data, cada um pela sua propriedade, com o valor vinculado
        Assert.Equal(pedidoId.ToString(), root.GetProperty("PedidoId").GetString());
        Assert.Equal("NAO-EXISTE", root.GetProperty("ProdutoSku").GetString());

        // o detail cita os dois critérios na ordem declarada (PedidoId antes de ProdutoSku)
        var detail = root.GetProperty("detail").GetString()!;
        Assert.Contains("PedidoItem", detail, StringComparison.Ordinal);
        Assert.True(
            detail.IndexOf("PedidoId", StringComparison.Ordinal) < detail.IndexOf("ProdutoSku", StringComparison.Ordinal),
            "os critérios devem aparecer na ordem declarada");
    }

    [Fact]
    public async Task Chave_composta_com_guid_malformado_nao_casa_a_rota_e_responde_404()
    {
        using var app = new DemoApiFactory();
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        // a constraint {pedidoId:guid} rejeita um segmento não-Guid: a rota não casa (404), sem 500
        var response = await client.GetAsync("/pedidos/nao-e-guid/itens/QUALQUER");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_declara_parametros_de_rota_e_respostas_das_buscas_por_chave()
    {
        using var app = new DemoApiFactory(environment: "Development");
        using var client = app.CreateClient();
        await app.ResetDatabaseAsync();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        // GET /produtos/por-sku/{sku} — parâmetro de rota 'sku' e 200/404
        var porSku = GetPath(paths, "/produtos/por-sku/{sku}").GetProperty("get");
        Assert.Contains(porSku.GetProperty("parameters").EnumerateArray(), parameter =>
            string.Equals(parameter.GetProperty("name").GetString(), "sku", StringComparison.OrdinalIgnoreCase) &&
            parameter.GetProperty("in").GetString() == "path");
        var porSkuResponses = porSku.GetProperty("responses");
        Assert.True(porSkuResponses.TryGetProperty("200", out _));
        AssertProblemDetailsResponse(porSkuResponses, "404");

        // GET /pedidos/{pedidoId}/itens/{produtoSku} — dois parâmetros de rota (chave composta)
        var itemPorSku = GetPath(paths, "/pedidos/{pedidoId}/itens/{produtoSku}").GetProperty("get");
        var parameters = itemPorSku.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Contains(parameters, parameter =>
            string.Equals(parameter.GetProperty("name").GetString(), "pedidoId", StringComparison.OrdinalIgnoreCase) &&
            parameter.GetProperty("in").GetString() == "path");
        Assert.Contains(parameters, parameter =>
            string.Equals(parameter.GetProperty("name").GetString(), "produtoSku", StringComparison.OrdinalIgnoreCase) &&
            parameter.GetProperty("in").GetString() == "path");
        var itemResponses = itemPorSku.GetProperty("responses");
        Assert.True(itemResponses.TryGetProperty("200", out _));
        AssertProblemDetailsResponse(itemResponses, "404");
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client, string nome, string sku, decimal preco)
    {
        var response = await client.PostAsJsonAsync("/produtos/", new { Nome = nome, Sku = sku, Preco = preco });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadApiJsonAsync<ProdutoResponse>();
        Assert.NotNull(created);
        return created.Id;
    }

    private static async Task RegistrarEstoqueInicialAsync(HttpClient client, Guid produtoId, int quantidade)
    {
        var response = await client.PostAsJsonAsync($"/produtos/{produtoId}/estoque", new { Quantidade = quantidade });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<Guid> CreatePedidoAsync(HttpClient client, Guid produtoId, int quantidade)
    {
        var response = await client.PostAsJsonAsync("/pedidos/", new
        {
            Itens = new[] { new { ProdutoId = produtoId, Quantidade = quantidade } }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadApiJsonAsync<PedidoCreatedResponse>();
        Assert.NotNull(created);
        return created.Id;
    }

    private static void AssertProblemDetailsResponse(JsonElement responses, string statusCode)
    {
        Assert.True(responses.TryGetProperty(statusCode, out var response),
            $"A resposta HTTP {statusCode} não foi declarada no OpenAPI.");
        Assert.True(response.GetProperty("content").TryGetProperty("application/problem+json", out _),
            $"A resposta HTTP {statusCode} não declara application/problem+json.");
    }

    private static JsonElement GetPath(JsonElement paths, string key)
    {
        if (paths.TryGetProperty(key, out var value))
            return value;

        var available = string.Join(", ", paths.EnumerateObject().Select(p => p.Name));
        throw new Xunit.Sdk.XunitException($"O path '{key}' não existe no OpenAPI. Paths disponíveis: {available}");
    }

    private sealed record ProdutoResponse(Guid Id);

    private sealed record PedidoCreatedResponse(Guid Id);

    private sealed record ProdutoPorSkuResponse(Guid Id, string Sku, string Nome);

    private sealed record ItemPedidoPorSkuResponse(Guid PedidoId, string ProdutoSku, int Quantidade);
}
