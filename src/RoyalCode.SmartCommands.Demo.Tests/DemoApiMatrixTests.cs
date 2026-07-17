using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartCommands.Demo.Tests.Support;

namespace RoyalCode.SmartCommands.Demo.Tests;

/// <summary>
/// Matriz end-to-end da Fase 9: cada verbo da superfície Minimal API gerada (GET, POST, PUT, PATCH,
/// DELETE, Find e Search), com e sem corpo, mais a metadata de autorização por policy.
/// </summary>
public class DemoApiMatrixTests
{
	[Fact]
	public async Task Matriz_Get_sem_corpo_com_parametro_externo_por_query()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		// SKU livre
		var disponivel = await client.GetAsync("/produtos/sku-disponivel?sku=SKU-LIVRE");
		Assert.Equal(HttpStatusCode.OK, disponivel.StatusCode);
		var livre = await disponivel.Content.ReadApiJsonAsync<SkuDisponibilidadeResponse>();
		Assert.NotNull(livre);
		Assert.Equal("SKU-LIVRE", livre.Sku);
		Assert.True(livre.Disponivel);

		// SKU em uso
		var create = await client.PostAsJsonAsync("/produtos/", new { Nome = "Produto A", Sku = "SKU-A", Preco = 10m });
		Assert.Equal(HttpStatusCode.Created, create.StatusCode);

		var emUso = await client.GetAsync("/produtos/sku-disponivel?sku=SKU-A");
		Assert.Equal(HttpStatusCode.OK, emUso.StatusCode);
		var usado = await emUso.Content.ReadApiJsonAsync<SkuDisponibilidadeResponse>();
		Assert.NotNull(usado);
		Assert.False(usado.Disponivel);

		// sem o parâmetro obrigatório: problema de parâmetro inválido (400)
		var semSku = await client.GetAsync("/produtos/sku-disponivel?sku=%20");
		Assert.Equal(HttpStatusCode.BadRequest, semSku.StatusCode);
	}

	[Fact]
	public async Task Matriz_Post_Put_Patch_com_corpo_e_Find_Search()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		// POST com corpo -> 201 Created + Location
		var create = await client.PostAsJsonAsync("/produtos/", new { Nome = "Produto M", Sku = "SKU-M", Preco = 10m });
		Assert.Equal(HttpStatusCode.Created, create.StatusCode);
		var created = await create.Content.ReadApiJsonAsync<ProdutoResponse>();
		Assert.NotNull(created);
		Assert.Equal($"produtos/{created.Id}", create.Headers.Location?.OriginalString);

		// POST sem corpo -> 400 (body obrigatório)
		var semCorpo = await client.PostAsync("/produtos/", null);
		Assert.Equal(HttpStatusCode.BadRequest, semCorpo.StatusCode);

		// PUT com corpo -> 200
		var edit = await client.PutAsJsonAsync($"/produtos/{created.Id}", new { Nome = "Produto M2", Preco = 12m });
		Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

		// PATCH sem corpo -> 200 (desativar) — comando age sobre a entidade da rota
		var patch = await client.PatchAsync($"/produtos/{created.Id}/desativar", null);
		Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

		// Find -> 200 com o estado atual; NotFound -> 404 ProblemDetails
		var find = await client.GetAsync($"/produtos/{created.Id}");
		Assert.Equal(HttpStatusCode.OK, find.StatusCode);
		var detalhes = await find.Content.ReadApiJsonAsync<ProdutoDetalhesResponse>();
		Assert.NotNull(detalhes);
		Assert.Equal("Produto M2", detalhes.Nome);
		Assert.False(detalhes.Ativo);

		var notFound = await client.GetAsync($"/produtos/{Guid.NewGuid()}");
		Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
		var problem = await notFound.Content.ReadApiTextAsync();
		Assert.Contains("status", problem, StringComparison.OrdinalIgnoreCase);

		// Search -> 200 (produto inativo aparece somente com incluirInativos)
		var search = await client.GetAsync("/produtos?nome=Produto&incluirInativos=true");
		Assert.Equal(HttpStatusCode.OK, search.StatusCode);
		var searchContent = await search.Content.ReadApiTextAsync();
		Assert.Contains(created.Id.ToString(), searchContent, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task Matriz_Delete_sem_corpo_responde_204_e_aplica_exclusao_logica()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var create = await client.PostAsJsonAsync("/lojas/", new { Nome = "Loja Matriz", Endereco = "Rua B, 1" });
		Assert.Equal(HttpStatusCode.Created, create.StatusCode);
		var created = await create.Content.ReadApiJsonAsync<LojaResponse>();
		Assert.NotNull(created);

		var delete = await client.DeleteAsync($"/lojas/{created.Id}");
		Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

		using var scope = app.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<DemoDbContext>();
		var loja = await db.Lojas.SingleAsync(l => l.Id == created.Id);
		Assert.False(loja.Ativa);

		// DELETE de loja inexistente -> 404
		var notFound = await client.DeleteAsync("/lojas/12345");
		Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
	}

	[Fact]
	public async Task Matriz_endpoint_com_policy_expoe_metadata_de_autorizacao()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var endpoint = app.Services.GetRequiredService<EndpointDataSource>()
			.Endpoints
			.OfType<RouteEndpoint>()
			.Single(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "relatorio-lojas");

		Assert.Equal("lojas/relatorio", endpoint.RoutePattern.RawText);

		var authorization = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
		Assert.Contains(authorization, data => data.Policy == "relatorios");
	}

	private sealed record SkuDisponibilidadeResponse(string Sku, bool Disponivel);

	private sealed record ProdutoResponse(Guid Id, string Nome, string Sku);

	private sealed record ProdutoDetalhesResponse(Guid Id, string Nome, bool Ativo);

	private sealed record LojaResponse(int Id, string Nome);
}
