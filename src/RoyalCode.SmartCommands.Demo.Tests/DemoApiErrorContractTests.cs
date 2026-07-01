using System.Net;
using System.Net.Http.Json;
using RoyalCode.SmartCommands.Demo.Tests.Support;

namespace RoyalCode.SmartCommands.Demo.Tests;

public class DemoApiErrorContractTests
{
	[Fact]
	public async Task CreateProduto_Must_Return_BadRequestProblem_When_ModelIsInvalid()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var response = await client.PostAsJsonAsync("/produtos/", new { Nome = "" });

		await response.AssertProblemAsync(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task CreateLoja_Must_Return_BadRequestProblem_When_ModelIsInvalid()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var response = await client.PostAsJsonAsync("/lojas/", new
		{
			Nome = "Loja Centro",
			Endereco = ""
		});

		await response.AssertProblemAsync(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task FindProduto_Must_Return_NotFoundProblem_When_EntityDoesNotExist()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var response = await client.GetAsync($"/produtos/{Guid.NewGuid()}");

		await response.AssertProblemAsync(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task EditProduto_Must_Return_NotFoundProblem_When_EntityDoesNotExist()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		// modelo valido (nome + preco) para passar da validacao e chegar na busca da entidade
		var response = await client.PutAsJsonAsync($"/produtos/{Guid.NewGuid()}", new { Nome = "Produto X", Preco = 10m });

		await response.AssertProblemAsync(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task EditProduto_Must_Return_BadRequestProblem_When_ModelIsInvalid()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var createResponse = await client.PostAsJsonAsync("/produtos/", new { Nome = "Produto A", Sku = "SKU-A", Preco = 10m });
		var created = await createResponse.Content.ReadApiJsonAsync<CreateProdutoResponse>();
		Assert.NotNull(created);

		var response = await client.PutAsJsonAsync($"/produtos/{created.Id}", new { Nome = "" });

		await response.AssertProblemAsync(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Endpoint_Must_Return_BadRequestProblem_When_RequestBodyIsMissing()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var createResponse = await client.PostAsJsonAsync("/produtos/", new { Nome = "Produto A", Sku = "SKU-A", Preco = 10m });
		var created = await createResponse.Content.ReadApiJsonAsync<CreateProdutoResponse>();
		Assert.NotNull(created);

		// EditarProduto tem corpo (Nome): uma requisição sem body é erro do cliente.
		// O guard gerado devolve 400 InvalidParameter em vez de NRE (500).
		var response = await client.PutAsync($"/produtos/{created.Id}", content: null);

		await response.AssertProblemAsync(HttpStatusCode.BadRequest);
	}

	private sealed record CreateProdutoResponse(Guid Id, string Nome);
}
