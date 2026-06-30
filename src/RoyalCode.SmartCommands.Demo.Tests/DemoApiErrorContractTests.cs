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

		var response = await client.PutAsJsonAsync($"/produtos/{Guid.NewGuid()}", new { Nome = "Produto X" });

		await response.AssertProblemAsync(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task EditProduto_Must_Return_BadRequestProblem_When_ModelIsInvalid()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var createResponse = await client.PostAsJsonAsync("/produtos/", new { Nome = "Produto A" });
		var created = await createResponse.Content.ReadApiJsonAsync<CreateProdutoResponse>();
		Assert.NotNull(created);

		var response = await client.PutAsJsonAsync($"/produtos/{created.Id}", new { Nome = "" });

		await response.AssertProblemAsync(HttpStatusCode.BadRequest);
	}

	private sealed record CreateProdutoResponse(Guid Id, string Nome);
}
