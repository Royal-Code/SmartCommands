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

	private sealed record LojaResponse(int Id, string Nome);
}
