using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.Demo.Tests.Support;

namespace RoyalCode.SmartCommands.Demo.Tests;

public class DemoApiEndpointTests
{
	[Fact]
	public async Task ProdutosEndpoints_Must_Create_Find_Edit_And_Search()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var createResponse = await client.PostAsJsonAsync("/produtos/", new { Nome = "Produto A", Sku = "SKU-A", Preco = 10m });

		Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
		Assert.Equal("produtos/", createResponse.Headers.Location?.OriginalString[..9]);

		var created = await createResponse.Content.ReadApiJsonAsync<CreateProdutoResponse>();
		Assert.NotNull(created);
		Assert.NotEqual(Guid.Empty, created.Id);
		Assert.Equal("Produto A", created.Nome);

		var findResponse = await client.GetAsync($"/produtos/{created.Id}");

		Assert.Equal(HttpStatusCode.OK, findResponse.StatusCode);
		var found = await findResponse.Content.ReadApiJsonAsync<ProdutoDetails>();
		Assert.NotNull(found);
		Assert.Equal(created.Id, found.Id);
		Assert.Equal("Produto A", found.Nome);
		Assert.True(found.Ativo);

		var editResponse = await client.PutAsJsonAsync($"/produtos/{created.Id}", new { Nome = "Produto B", Preco = 15m });

		Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);

		var afterEditResponse = await client.GetAsync($"/produtos/{created.Id}");
		var afterEdit = await afterEditResponse.Content.ReadApiJsonAsync<ProdutoDetails>();
		Assert.NotNull(afterEdit);
		Assert.Equal("Produto B", afterEdit.Nome);

		await AssertSearchContainsProductAsync(client, "/produtos?nome=Produto", created.Id, "Produto B");
		await AssertSearchContainsProductAsync(client, "/produtos/search/manual?nome=Produto", created.Id, "Produto B");
		await AssertSearchContainsProductAsync(client, "/produtos/search/auto?nome=Produto", created.Id, "Produto B");
		await AssertSearchContainsProductAsync(client, "/produtos/filtro/7?nome=Produto", created.Id, "Produto B");
	}

	[Fact]
	public async Task LojasEndpoints_Must_Create_Loja()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var response = await client.PostAsJsonAsync("/lojas/", new
		{
			Nome = "Loja Centro",
			Endereco = "Rua A, 100"
		});

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		Assert.Equal("lojas/1", response.Headers.Location?.OriginalString);

		var created = await response.Content.ReadApiJsonAsync<CreateLojaResponse>();
		Assert.NotNull(created);
		Assert.Equal(1, created.Id);
		Assert.Equal("Loja Centro", created.Nome);
	}

	[Fact]
	public async Task GeneratedRoutes_Must_Expose_Expected_RoutePatterns_And_Names()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var routes = app.Services.GetRequiredService<EndpointDataSource>()
			.Endpoints
			.OfType<RouteEndpoint>()
			.Select(e => new
			{
				Pattern = e.RoutePattern.RawText,
				Name = e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
			})
			.ToArray();

		Assert.Contains(routes, r => r.Pattern == "produtos/" && r.Name == "criar-produto");
		Assert.Contains(routes, r => r.Pattern == "produtos/{id}" && r.Name == "Editar Produto");
		Assert.Contains(routes, r => r.Pattern == "produtos/{id:guid}" && r.Name == "Get product details");
		Assert.Contains(routes, r => r.Pattern == "produtos/" && r.Name == "Listagem paginada de produtos");
		Assert.Contains(routes, r => r.Pattern == "produtos/filtro/{id:int}" && r.Name == "Listagem paginada de produtos exemplos");
		Assert.Contains(routes, r => r.Pattern == "pedidos/" && r.Name == "criar-pedido");
		Assert.Contains(routes, r => r.Pattern == "pedidos/{id:guid}/cancelar" && r.Name == "cancelar-pedido");
		Assert.Contains(routes, r => r.Pattern == "pedidos/{id:guid}" && r.Name == "Get order details");
		Assert.Contains(routes, r => r.Pattern == "pedidos/" && r.Name == "Listagem paginada de pedidos");
		Assert.Contains(routes, r => r.Pattern == "lojas/" && r.Name == "loja-criar");
	}

	private static async Task AssertSearchContainsProductAsync(
		HttpClient client,
		string route,
		Guid productId,
		string productName)
	{
		var response = await client.GetAsync(route);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var content = await response.Content.ReadApiTextAsync();
		Assert.Contains(productId.ToString(), content, StringComparison.OrdinalIgnoreCase);
		Assert.Contains(productName, content, StringComparison.OrdinalIgnoreCase);
	}

	private sealed record CreateProdutoResponse(Guid Id, string Nome);

	private sealed record ProdutoDetails(Guid Id, string Nome, bool Ativo);

	private sealed record CreateLojaResponse(int Id, string Nome);
}
