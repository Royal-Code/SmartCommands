using System.Net;
using System.Net.Http.Json;
using RoyalCode.SmartCommands.Demo.Tests.Support;

namespace RoyalCode.SmartCommands.Demo.Tests;

// Fase 5 do plan-demo-usage-scenarios: soft delete / desativacao. Transicoes de estado nao idempotentes
// (desativar ja inativo / reativar ja ativo -> 409, decisao da fase), listagem publica que esconde inativos
// por padrao e busca administrativa que os inclui via ?incluirInativos=true.
public class SoftDeleteTests
{
	[Fact]
	public async Task DesativarProduto_Ativo_Retorna200_E_MarcaInativo()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var id = await CreateProductAsync(client, "Camiseta", "CAM-001", 40m);

		var response = await client.PatchAsync($"/produtos/{id}/desativar", content: null);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var details = await GetDetailsAsync(client, id);
		Assert.False(details.Ativo);
	}

	[Fact]
	public async Task DesativarProduto_JaInativo_Retorna409()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var id = await CreateProductAsync(client, "Camiseta", "CAM-001", 40m);
		var primeira = await client.PatchAsync($"/produtos/{id}/desativar", content: null);
		Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);

		var segunda = await client.PatchAsync($"/produtos/{id}/desativar", content: null);

		await segunda.AssertProblemAsync(HttpStatusCode.Conflict, "ja esta inativo");
	}

	[Fact]
	public async Task ReativarProduto_Inativo_Retorna200_E_MarcaAtivo()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var id = await CreateProductAsync(client, "Camiseta", "CAM-001", 40m);
		var desativar = await client.PatchAsync($"/produtos/{id}/desativar", content: null);
		Assert.Equal(HttpStatusCode.OK, desativar.StatusCode);

		var reativar = await client.PatchAsync($"/produtos/{id}/reativar", content: null);

		Assert.Equal(HttpStatusCode.OK, reativar.StatusCode);
		var details = await GetDetailsAsync(client, id);
		Assert.True(details.Ativo);
	}

	[Fact]
	public async Task ReativarProduto_JaAtivo_Retorna409()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var id = await CreateProductAsync(client, "Camiseta", "CAM-001", 40m);

		var response = await client.PatchAsync($"/produtos/{id}/reativar", content: null);

		await response.AssertProblemAsync(HttpStatusCode.Conflict, "ja esta ativo");
	}

	[Fact]
	public async Task DetalhesDeProdutoInativo_Retorna200_ComAtivoFalse()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		// Decisao da fase: soft delete esconde da listagem publica, mas o find por id continua acessivel.
		var id = await CreateProductAsync(client, "Camiseta", "CAM-001", 40m);
		await client.PatchAsync($"/produtos/{id}/desativar", content: null);

		var details = await GetDetailsAsync(client, id);

		Assert.Equal("CAM-001", details.Sku);
		Assert.False(details.Ativo);
	}

	[Fact]
	public async Task ListagemPublica_EscondeInativosPorPadrao()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		await CreateProductAsync(client, "Ativo", "ATV-001", 40m);
		var inativoId = await CreateProductAsync(client, "Inativo", "INA-002", 50m);
		await client.PatchAsync($"/produtos/{inativoId}/desativar", content: null);

		var listagem = await (await client.GetAsync("/produtos")).Content.ReadApiTextAsync();

		Assert.Contains("ATV-001", listagem);
		Assert.DoesNotContain("INA-002", listagem);
	}

	[Fact]
	public async Task BuscaAdministrativa_IncluirInativos_RetornaInativos()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		await CreateProductAsync(client, "Ativo", "ATV-001", 40m);
		var inativoId = await CreateProductAsync(client, "Inativo", "INA-002", 50m);
		await client.PatchAsync($"/produtos/{inativoId}/desativar", content: null);

		var listagem = await (await client.GetAsync("/produtos?incluirInativos=true")).Content.ReadApiTextAsync();

		Assert.Contains("ATV-001", listagem);
		Assert.Contains("INA-002", listagem);
	}

	[Fact]
	public async Task BuscaAdministrativa_AtivoFalse_RetornaSomenteInativos()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		await CreateProductAsync(client, "Ativo", "ATV-001", 40m);
		var inativoId = await CreateProductAsync(client, "Inativo", "INA-002", 50m);
		await client.PatchAsync($"/produtos/{inativoId}/desativar", content: null);

		var listagem = await (await client.GetAsync("/produtos?ativo=false")).Content.ReadApiTextAsync();

		Assert.DoesNotContain("ATV-001", listagem);
		Assert.Contains("INA-002", listagem);
	}

	private static async Task<Guid> CreateProductAsync(HttpClient client, string nome, string sku, decimal preco)
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

	private sealed record ProdutoResponse(Guid Id);

	private sealed record ProdutoDetails(Guid Id, string Nome, string Sku, decimal Preco, bool Ativo);
}
