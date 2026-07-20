using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartCommands.Demo.Tests.Support;

namespace RoyalCode.SmartCommands.Demo.Tests;

/// <summary>
/// Fase 10 (DF23): filtros de endpoint na ordem declarada e ativados por DI, status explícito
/// (204 descartando o valor, 201 sem Location) e problemas preservados pela seleção de status.
/// </summary>
public class DemoHttpExtensibilityTests
{
	[Fact]
	public async Task NoContent_explicito_descarta_o_valor_e_executa_os_filtros_na_ordem()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var create = await client.PostAsJsonAsync("/lojas/", new { Nome = "Loja Original", Endereco = "Rua C, 3" });
		Assert.Equal(HttpStatusCode.Created, create.StatusCode);
		var created = await create.Content.ReadApiJsonAsync<LojaResponse>();
		Assert.NotNull(created);

		var rename = await client.PutAsJsonAsync($"/lojas/{created.Id}/nome", new { Nome = "Loja Renomeada" });

		// 204: o valor de sucesso (Result<Loja>) foi descartado deliberadamente
		Assert.Equal(HttpStatusCode.NoContent, rename.StatusCode);
		Assert.Equal(0, rename.Content.Headers.ContentLength ?? 0);

		// filtros na ordem declarada: auditoria e depois carimbo (este ativado com dependência de DI)
		Assert.True(rename.Headers.TryGetValues("X-Demo-Filtros", out var filtros));
		Assert.Equal(["auditoria", "carimbo"], filtros!.ToArray());
		Assert.True(rename.Headers.Contains("X-Demo-Carimbo"));

		// o efeito do comando foi persistido
		using var scope = app.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<DemoDbContext>();
		var loja = await db.Lojas.SingleAsync(l => l.Id == created.Id);
		Assert.Equal("Loja Renomeada", loja.Nome);
	}

	[Fact]
	public async Task NoContent_explicito_preserva_os_problemas()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		// corpo inválido -> 400 (HasProblems), nunca 204
		var invalid = await client.PutAsJsonAsync("/lojas/1/nome", new { Nome = "" });
		Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

		// loja inexistente -> 404 (NotFound do EditEntity), nunca 204
		var notFound = await client.PutAsJsonAsync("/lojas/12345/nome", new { Nome = "Qualquer" });
		Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
	}

	[Fact]
	public async Task Created_explicito_sem_MapCreatedRoute_responde_201_sem_Location_com_corpo_projetado()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var import = await client.PostAsJsonAsync("/lojas/importadas", new
		{
			Nome = "Loja Importada",
			Endereco = "Rua D, 4"
		});

		Assert.Equal(HttpStatusCode.Created, import.StatusCode);
		Assert.Null(import.Headers.Location);

		var body = await import.Content.ReadApiJsonAsync<LojaResponse>();
		Assert.NotNull(body);
		Assert.True(body.Id > 0);
		Assert.Equal("Loja Importada", body.Nome);
	}

	[Fact]
	public async Task Accepted_com_MapAcceptedRoute_responde_202_com_Location_e_corpo_projetado()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var accepted = await client.PostAsJsonAsync("/lojas/agendamentos", new
		{
			Nome = "Loja Agendada / Sul?#",
			Endereco = "Rua E, 5"
		});

		// 202 com Location montado a partir do valor de sucesso e corpo projetado por MapResponseValues
		Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);

		var body = await accepted.Content.ReadApiJsonAsync<LojaResponse>();
		Assert.NotNull(body);
		Assert.True(body.Id > 0);
		Assert.Equal("Loja Agendada / Sul?#", body.Nome);

		Assert.NotNull(accepted.Headers.Location);
		Assert.Equal(
			$"lojas/agendamentos/{body.Id}/Loja%20Agendada%20%2F%20Sul%3F%23/status",
			accepted.Headers.Location!.OriginalString);
	}

	[Fact]
	public async Task Accepted_com_problema_preserva_status_e_nao_emite_Location()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var response = await client.PostAsJsonAsync("/lojas/agendamentos", new
		{
			Nome = "",
			Endereco = ""
		});

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Null(response.Headers.Location);
	}

	[Fact]
	public async Task Accepted_com_rota_estatica_e_Result_responde_202_com_Location_fixo_sem_corpo()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var accepted = await client.PostAsJsonAsync("/lojas/reindexacoes", new { });

		// 202 sem corpo (Result sem valor) e com Location estático
		Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
		Assert.Equal(0, accepted.Content.Headers.ContentLength ?? 0);
		Assert.NotNull(accepted.Headers.Location);
		Assert.Equal("lojas/reindexacoes/status", accepted.Headers.Location!.OriginalString);
	}

	[Fact]
	public async Task Ok_explicito_sobrescreve_a_inferencia_de_NoContent_do_Delete()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();

		var response = await client.DeleteAsync("/lojas/cache");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task Filtro_de_Search_e_executado_no_endpoint_real()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var create = await client.PostAsJsonAsync(
			"/produtos/",
			new { Nome = "Produto Filtrado", Sku = "FILTRO-001", Preco = 10m });
		Assert.Equal(HttpStatusCode.Created, create.StatusCode);

		var response = await client.GetAsync("/produtos");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.True(response.Headers.TryGetValues("X-Demo-Filtros", out var filtros));
		Assert.Equal(["auditoria"], filtros!.ToArray());
	}

	private sealed record LojaResponse(int Id, string Nome);
}
