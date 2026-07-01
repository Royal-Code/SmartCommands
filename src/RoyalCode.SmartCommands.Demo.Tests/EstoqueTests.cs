using System.Net;
using System.Net.Http.Json;
using RoyalCode.SmartCommands.Demo.Tests.Support;

namespace RoyalCode.SmartCommands.Demo.Tests;

// Fase 2 do plan-demo-usage-scenarios: estoque com saldo disponivel/reservado, problemas de negocio
// e retry de concorrencia sobre uma entidade propria da demo com token Version em SQLite.
public class EstoqueTests
{
	[Fact]
	public async Task AdicionarEntrada_AumentaSaldoDisponivel()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client);
		await RegistrarEstoqueInicialAsync(client, produtoId, 10);

		var entrada = await client.PostAsJsonAsync($"/produtos/{produtoId}/estoque/entradas", new { Quantidade = 5 });

		Assert.Equal(HttpStatusCode.OK, entrada.StatusCode);
		var estoque = await GetEstoqueAsync(client, produtoId);
		Assert.Equal(produtoId, estoque.ProdutoId);
		Assert.Equal(15, estoque.Disponivel);
		Assert.Equal(0, estoque.Reservado);
	}

	[Fact]
	public async Task Reservar_ReduzDisponivel_E_AumentaReservado()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client);
		await RegistrarEstoqueInicialAsync(client, produtoId, 10);

		var reserva = await client.PostAsJsonAsync($"/produtos/{produtoId}/estoque/reservas", new { Quantidade = 4 });

		Assert.Equal(HttpStatusCode.OK, reserva.StatusCode);
		var estoque = await GetEstoqueAsync(client, produtoId);
		Assert.Equal(6, estoque.Disponivel);
		Assert.Equal(4, estoque.Reservado);
	}

	[Fact]
	public async Task LiberarReserva_ReduzReservado_E_AumentaDisponivel()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client);
		await RegistrarEstoqueInicialAsync(client, produtoId, 10);
		await ReservarAsync(client, produtoId, 4);

		var liberacao = await client.PostAsJsonAsync($"/produtos/{produtoId}/estoque/liberacoes", new { Quantidade = 3 });

		Assert.Equal(HttpStatusCode.OK, liberacao.StatusCode);
		var estoque = await GetEstoqueAsync(client, produtoId);
		Assert.Equal(9, estoque.Disponivel);
		Assert.Equal(1, estoque.Reservado);
	}

	[Fact]
	public async Task Reservar_AcimaDoDisponivel_RetornaProblema()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client);
		await RegistrarEstoqueInicialAsync(client, produtoId, 2);

		var reserva = await client.PostAsJsonAsync($"/produtos/{produtoId}/estoque/reservas", new { Quantidade = 3 });

		await reserva.AssertProblemAsync(HttpStatusCode.Conflict, "Estoque insuficiente");
	}

	[Fact]
	public async Task Reservar_ProdutoInexistente_Retorna404()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var reserva = await client.PostAsJsonAsync($"/produtos/{Guid.NewGuid()}/estoque/reservas", new { Quantidade = 1 });

		await reserva.AssertProblemAsync(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task ConsultarEstoque_SemRegistro_RetornaSaldoZerado()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client);

		var response = await client.GetAsync($"/produtos/{produtoId}/estoque");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var estoque = await response.Content.ReadApiJsonAsync<EstoqueResponse>();
		Assert.NotNull(estoque);
		Assert.Equal(produtoId, estoque.ProdutoId);
		Assert.Equal(0, estoque.Disponivel);
		Assert.Equal(0, estoque.Reservado);
		Assert.Equal(0, estoque.Version);
	}

	[Fact]
	public async Task Reservar_ComConflitoTransitorio_TentaNovamente_E_Conclui()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client);
		await RegistrarEstoqueInicialAsync(client, produtoId, 10);
		app.ConcurrencyFailures.FailNextStockSave();

		var reserva = await client.PostAsJsonAsync($"/produtos/{produtoId}/estoque/reservas", new { Quantidade = 2 });

		Assert.Equal(HttpStatusCode.OK, reserva.StatusCode);
		Assert.Equal(1, app.ConcurrencyFailures.Failures);
		var estoque = await GetEstoqueAsync(client, produtoId);
		Assert.Equal(8, estoque.Disponivel);
		Assert.Equal(2, estoque.Reservado);
	}

	[Fact]
	public async Task Reservar_ComConflitoPersistente_RetornaProblemDaOperationKey()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client);
		await RegistrarEstoqueInicialAsync(client, produtoId, 10);
		app.ConcurrencyFailures.FailStockSavesAlways();

		var reserva = await client.PostAsJsonAsync($"/produtos/{produtoId}/estoque/reservas", new { Quantidade = 2 });

		await reserva.AssertProblemAsync(HttpStatusCode.Conflict, "O estoque foi alterado");
		Assert.Equal(3, app.ConcurrencyFailures.Failures);

		var estoque = await GetEstoqueAsync(client, produtoId);
		Assert.Equal(10, estoque.Disponivel);
		Assert.Equal(0, estoque.Reservado);
	}

	private static async Task<Guid> CreateProductAsync(HttpClient client)
	{
		var create = await client.PostAsJsonAsync("/produtos/", new
		{
			Nome = $"Produto {Guid.NewGuid():N}",
			Sku = $"SKU-{Guid.NewGuid():N}",
			Preco = 10m
		});

		Assert.Equal(HttpStatusCode.Created, create.StatusCode);
		var created = await create.Content.ReadApiJsonAsync<ProdutoResponse>();
		Assert.NotNull(created);
		return created.Id;
	}

	private static async Task<EstoqueResponse> RegistrarEstoqueInicialAsync(HttpClient client, Guid produtoId, int quantidade)
	{
		var response = await client.PostAsJsonAsync($"/produtos/{produtoId}/estoque", new { Quantidade = quantidade });

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		return await GetEstoqueAsync(client, produtoId);
	}

	private static async Task<EstoqueResponse> ReservarAsync(HttpClient client, Guid produtoId, int quantidade)
	{
		var response = await client.PostAsJsonAsync($"/produtos/{produtoId}/estoque/reservas", new { Quantidade = quantidade });

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		return await GetEstoqueAsync(client, produtoId);
	}

	private static async Task<EstoqueResponse> GetEstoqueAsync(HttpClient client, Guid produtoId)
	{
		var response = await client.GetAsync($"/produtos/{produtoId}/estoque");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var estoque = await response.Content.ReadApiJsonAsync<EstoqueResponse>();
		Assert.NotNull(estoque);
		return estoque;
	}

	private sealed record ProdutoResponse(Guid Id);

	private sealed record EstoqueResponse(Guid ProdutoId, int Disponivel, int Reservado, int Version);
}
