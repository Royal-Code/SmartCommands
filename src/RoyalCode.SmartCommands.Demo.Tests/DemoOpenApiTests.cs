using System.Net;
using System.Text.Json;
using RoyalCode.SmartCommands.Demo.Tests.Support;

namespace RoyalCode.SmartCommands.Demo.Tests;

/// <summary>
/// Fase 9 (T9): inspeção do JSON OpenAPI gerado — corpo obrigatório, parâmetros, status de sucesso e
/// ProblemDetails precisam refletir os endpoints reais produzidos pelo generator.
/// </summary>
public class DemoOpenApiTests
{
	[Fact]
	public async Task OpenApi_reflete_body_parametros_status_e_problem_details()
	{
		// o Swagger só é publicado no ambiente Development (ver ConfigurePipeline)
		using var app = new DemoApiFactory(environment: "Development");
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var response = await client.GetAsync("/swagger/v1/swagger.json");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var paths = document.RootElement.GetProperty("paths");

		// POST /produtos — corpo de requisição e 201 Created (a barra final é normalizada pelo OpenAPI)
		var createProduto = GetPath(paths, "/produtos").GetProperty("post");
		Assert.True(createProduto.TryGetProperty("requestBody", out var requestBody));
		Assert.True(requestBody.TryGetProperty("content", out _));
		var createResponses = createProduto.GetProperty("responses");
		Assert.True(createResponses.TryGetProperty("201", out _));
		AssertProblemDetailsResponse(createResponses, "400");

		// Find /produtos/{id} — parâmetro de rota e 404 NotFound
		var findProduto = GetPath(paths, "/produtos/{id}").GetProperty("get");
		var findParameters = findProduto.GetProperty("parameters").EnumerateArray().ToArray();
		Assert.Contains(findParameters, parameter =>
			parameter.GetProperty("name").GetString() == "id" &&
			parameter.GetProperty("in").GetString() == "path");
		var findResponses = findProduto.GetProperty("responses");
		Assert.True(findResponses.TryGetProperty("200", out _));
		AssertProblemDetailsResponse(findResponses, "404");

		// Search /produtos — parâmetros de query do filtro ([AsParameters])
		var searchProduto = GetPath(paths, "/produtos").GetProperty("get");
		var searchParameters = searchProduto.GetProperty("parameters").EnumerateArray().ToArray();
		Assert.Contains(searchParameters, parameter =>
			string.Equals(parameter.GetProperty("name").GetString(), "nome", StringComparison.OrdinalIgnoreCase) &&
			parameter.GetProperty("in").GetString() == "query");

		// GET /produtos/sku-disponivel — parâmetro externo por query (DF2), sem requestBody
		var skuDisponivel = GetPath(paths, "/produtos/sku-disponivel").GetProperty("get");
		Assert.False(skuDisponivel.TryGetProperty("requestBody", out _));
		var skuParameters = skuDisponivel.GetProperty("parameters").EnumerateArray().ToArray();
		Assert.Contains(skuParameters, parameter =>
			parameter.GetProperty("name").GetString() == "sku" &&
			parameter.GetProperty("in").GetString() == "query");

		// DELETE /lojas/{id} — 204 No Content, sem requestBody
		var excluirLoja = GetPath(paths, "/lojas/{id}").GetProperty("delete");
		Assert.False(excluirLoja.TryGetProperty("requestBody", out _));
		var excluirResponses = excluirLoja.GetProperty("responses");
		Assert.True(excluirResponses.TryGetProperty("204", out _),
			"Respostas do DELETE /lojas/{id}: " +
			string.Join(", ", excluirResponses.EnumerateObject().Select(p => p.Name)));

	}

	[Fact]
	public async Task OpenApi_reflete_tags_e_status_explicitos_da_extensibilidade()
	{
		using var app = new DemoApiFactory(environment: "Development");
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var response = await client.GetAsync("/swagger/v1/swagger.json");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var paths = document.RootElement.GetProperty("paths");

		// PUT /lojas/{id}/nome — WithResultStatus(NoContent): 204 sem corpo; tags na ordem declarada
		var renomear = GetPath(paths, "/lojas/{id}/nome").GetProperty("put");
		var renomearResponses = renomear.GetProperty("responses");
		Assert.True(renomearResponses.TryGetProperty("204", out _),
			"Respostas do PUT /lojas/{id}/nome: " +
			string.Join(", ", renomearResponses.EnumerateObject().Select(p => p.Name)));
		AssertProblemDetailsResponse(renomearResponses, "404");
		var renomearTags = renomear.GetProperty("tags").EnumerateArray()
			.Select(tag => tag.GetString()!)
			.ToArray();
		Assert.Equal(["Lojas", "Administracao"], renomearTags);

		// POST /lojas/importadas — WithResultStatus(Created) sem MapCreatedRoute: 201 com corpo projetado
		var importar = GetPath(paths, "/lojas/importadas").GetProperty("post");
		var importarResponses = importar.GetProperty("responses");
		Assert.True(importarResponses.TryGetProperty("201", out var created201),
			"Respostas do POST /lojas/importadas: " +
			string.Join(", ", importarResponses.EnumerateObject().Select(p => p.Name)));
		Assert.True(created201.TryGetProperty("content", out _));

		// DELETE /lojas/cache — WithResultStatus(Ok) sobrepõe a inferência 204 do verbo DELETE
		var invalidarCache = GetPath(paths, "/lojas/cache").GetProperty("delete");
		var invalidarCacheResponses = invalidarCache.GetProperty("responses");
		Assert.True(invalidarCacheResponses.TryGetProperty("200", out _),
			"Respostas do DELETE /lojas/cache: " +
			string.Join(", ", invalidarCacheResponses.EnumerateObject().Select(p => p.Name)));
		Assert.False(invalidarCacheResponses.TryGetProperty("204", out _));
	}

	private static void AssertProblemDetailsResponse(JsonElement responses, string statusCode)
	{
		Assert.True(responses.TryGetProperty(statusCode, out var response),
			$"A resposta HTTP {statusCode} não foi declarada no OpenAPI.");
		Assert.True(response.GetProperty("content").TryGetProperty("application/problem+json", out var content),
			$"A resposta HTTP {statusCode} não declara application/problem+json.");
		Assert.Contains("ProblemDetails", content.GetRawText(), StringComparison.Ordinal);
	}

	private static JsonElement GetPath(JsonElement paths, string key)
	{
		if (paths.TryGetProperty(key, out var value))
			return value;

		var available = string.Join(", ", paths.EnumerateObject().Select(p => p.Name));
		throw new Xunit.Sdk.XunitException($"O path '{key}' não existe no OpenAPI. Paths disponíveis: {available}");
	}
}
