using System.Net;
using System.Net.Http.Json;
using RoyalCode.SmartCommands.Demo.Tests.Support;

namespace RoyalCode.SmartCommands.Demo.Tests;

// Fase 3 do plan-demo-usage-scenarios: pedido simples combinando catalogo + estoque em uma
// transacao, com reserva na criacao, liberacao no cancelamento e consulta/listagem por status.
public class PedidoTests
{
	[Fact]
	public async Task CriarPedido_Valido_Retorna201_CriaPedido_E_ReservaEstoque()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client, "Camiseta", "CAM-001", 25m);
		await RegistrarEstoqueInicialAsync(client, produtoId, 10);

		var response = await client.PostAsJsonAsync("/pedidos/", new
		{
			Itens = new[]
			{
				new { ProdutoId = produtoId, Quantidade = 3 }
			}
		});

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		Assert.Equal("pedidos/", response.Headers.Location?.OriginalString[..8]);

		var created = await response.Content.ReadApiJsonAsync<PedidoCreatedResponse>();
		Assert.NotNull(created);
		Assert.NotEqual(Guid.Empty, created.Id);
		Assert.Equal(PedidoStatusResponse.Aberto, created.Status);
		Assert.Equal(75m, created.Total);

		var estoque = await GetEstoqueAsync(client, produtoId);
		Assert.Equal(7, estoque.Disponivel);
		Assert.Equal(3, estoque.Reservado);

		var details = await GetPedidoAsync(client, created.Id);
		Assert.Equal(created.Id, details.Id);
		Assert.Equal(PedidoStatusResponse.Aberto, details.Status);
		Assert.Equal(75m, details.Total);
		var item = Assert.Single(details.Itens);
		Assert.Equal(produtoId, item.ProdutoId);
		Assert.Equal("Camiseta", item.ProdutoNome);
		Assert.Equal("CAM-001", item.ProdutoSku);
		Assert.Equal(3, item.Quantidade);
		Assert.Equal(25m, item.PrecoUnitario);
		Assert.Equal(75m, item.Total);
	}

	[Fact]
	public async Task CriarPedido_ComItemInvalido_Retorna400()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var response = await client.PostAsJsonAsync("/pedidos/", new
		{
			Itens = new[]
			{
				new { ProdutoId = Guid.Empty, Quantidade = 0 }
			}
		});

		await response.AssertProblemAsync(HttpStatusCode.BadRequest, "quantidade");
	}

	[Fact]
	public async Task CriarPedido_ComProdutoInexistente_Retorna400()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = Guid.NewGuid();

		var response = await client.PostAsJsonAsync("/pedidos/", new
		{
			Itens = new[]
			{
				new { ProdutoId = produtoId, Quantidade = 1 }
			}
		});

		await response.AssertProblemAsync(HttpStatusCode.BadRequest, "nao encontrado");
	}

	[Fact]
	public async Task CriarPedido_ComProdutoInativo_RetornaProblemaDeNegocio()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client, "Bermuda", "BER-001", 40m);
		await RegistrarEstoqueInicialAsync(client, produtoId, 10);
		var desativar = await client.PatchAsync($"/produtos/{produtoId}/desativar", content: null);
		Assert.Equal(HttpStatusCode.OK, desativar.StatusCode);

		var response = await client.PostAsJsonAsync("/pedidos/", new
		{
			Itens = new[]
			{
				new { ProdutoId = produtoId, Quantidade = 1 }
			}
		});

		await response.AssertProblemAsync(HttpStatusCode.Conflict, "inativo");

		var estoque = await GetEstoqueAsync(client, produtoId);
		Assert.Equal(10, estoque.Disponivel);
		Assert.Equal(0, estoque.Reservado);
	}

	[Fact]
	public async Task CriarPedido_ComProdutoSemEstoqueRegistrado_RetornaProblemaDeNegocio()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client, "Jaqueta", "JAQ-001", 150m);

		var response = await client.PostAsJsonAsync("/pedidos/", new
		{
			Itens = new[]
			{
				new { ProdutoId = produtoId, Quantidade = 1 }
			}
		});

		await response.AssertProblemAsync(HttpStatusCode.Conflict, "ainda nao foi registrado");

		var list = await (await client.GetAsync("/pedidos")).Content.ReadApiTextAsync();
		Assert.DoesNotContain(produtoId.ToString(), list, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task CriarPedido_SemEstoqueSuficiente_RetornaProblema_E_NaoPersistePedidoParcial()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client, "Tenis", "TEN-001", 120m);
		await RegistrarEstoqueInicialAsync(client, produtoId, 2);

		var response = await client.PostAsJsonAsync("/pedidos/", new
		{
			Itens = new[]
			{
				new { ProdutoId = produtoId, Quantidade = 3 }
			}
		});

		await response.AssertProblemAsync(HttpStatusCode.Conflict, "Estoque insuficiente");

		var estoque = await GetEstoqueAsync(client, produtoId);
		Assert.Equal(2, estoque.Disponivel);
		Assert.Equal(0, estoque.Reservado);

		var list = await (await client.GetAsync("/pedidos")).Content.ReadApiTextAsync();
		Assert.DoesNotContain(produtoId.ToString(), list, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task CancelarPedido_LiberaEstoqueReservado()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client, "Mochila", "MOC-001", 80m);
		await RegistrarEstoqueInicialAsync(client, produtoId, 10);
		var pedido = await CreatePedidoAsync(client, produtoId, 4);

		var response = await client.PatchAsync($"/pedidos/{pedido.Id}/cancelar", content: null);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var estoque = await GetEstoqueAsync(client, produtoId);
		Assert.Equal(10, estoque.Disponivel);
		Assert.Equal(0, estoque.Reservado);

		var details = await GetPedidoAsync(client, pedido.Id);
		Assert.Equal(PedidoStatusResponse.Cancelado, details.Status);
	}

	[Fact]
	public async Task CancelarPedido_JaCancelado_RetornaProblemaDeNegocio()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client, "Carteira", "CAR-001", 30m);
		await RegistrarEstoqueInicialAsync(client, produtoId, 10);
		var pedido = await CreatePedidoAsync(client, produtoId, 2);
		var primeiroCancelamento = await client.PatchAsync($"/pedidos/{pedido.Id}/cancelar", content: null);
		Assert.Equal(HttpStatusCode.OK, primeiroCancelamento.StatusCode);

		var response = await client.PatchAsync($"/pedidos/{pedido.Id}/cancelar", content: null);

		await response.AssertProblemAsync(HttpStatusCode.Conflict, "ja esta cancelado");

		var estoque = await GetEstoqueAsync(client, produtoId);
		Assert.Equal(10, estoque.Disponivel);
		Assert.Equal(0, estoque.Reservado);
	}

	[Fact]
	public async Task CancelarPedido_Inexistente_Retorna404()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var response = await client.PatchAsync($"/pedidos/{Guid.NewGuid()}/cancelar", content: null);

		await response.AssertProblemAsync(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task CancelarPedido_ComConflitoTransitorio_TentaNovamente_E_LiberaReserva()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var produtoId = await CreateProductAsync(client, "Bolsa", "BOL-001", 90m);
		await RegistrarEstoqueInicialAsync(client, produtoId, 10);
		var pedido = await CreatePedidoAsync(client, produtoId, 2);
		app.ConcurrencyFailures.FailNextStockSave();

		var response = await client.PatchAsync($"/pedidos/{pedido.Id}/cancelar", content: null);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(1, app.ConcurrencyFailures.Failures);

		var estoque = await GetEstoqueAsync(client, produtoId);
		Assert.Equal(10, estoque.Disponivel);
		Assert.Equal(0, estoque.Reservado);

		var details = await GetPedidoAsync(client, pedido.Id);
		Assert.Equal(PedidoStatusResponse.Cancelado, details.Status);
	}

	[Fact]
	public async Task ListarPedidos_FiltraPorStatus()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var primeiroProdutoId = await CreateProductAsync(client, "Camisa", "CAM-002", 20m);
		var segundoProdutoId = await CreateProductAsync(client, "Calca", "CAL-001", 60m);
		await RegistrarEstoqueInicialAsync(client, primeiroProdutoId, 10);
		await RegistrarEstoqueInicialAsync(client, segundoProdutoId, 10);
		var aberto = await CreatePedidoAsync(client, primeiroProdutoId, 1);
		var cancelado = await CreatePedidoAsync(client, segundoProdutoId, 1);
		var cancelar = await client.PatchAsync($"/pedidos/{cancelado.Id}/cancelar", content: null);
		Assert.Equal(HttpStatusCode.OK, cancelar.StatusCode);

		var list = await (await client.GetAsync("/pedidos?status=Cancelado")).Content.ReadApiTextAsync();

		Assert.Contains(cancelado.Id.ToString(), list, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain(aberto.Id.ToString(), list, StringComparison.OrdinalIgnoreCase);
	}

	private static async Task<Guid> CreateProductAsync(
		HttpClient client,
		string nome,
		string sku,
		decimal preco)
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

	private static async Task<PedidoCreatedResponse> CreatePedidoAsync(
		HttpClient client,
		Guid produtoId,
		int quantidade)
	{
		var response = await client.PostAsJsonAsync("/pedidos/", new
		{
			Itens = new[]
			{
				new { ProdutoId = produtoId, Quantidade = quantidade }
			}
		});

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		var created = await response.Content.ReadApiJsonAsync<PedidoCreatedResponse>();
		Assert.NotNull(created);
		return created;
	}

	private static async Task<PedidoDetailsResponse> GetPedidoAsync(HttpClient client, Guid pedidoId)
	{
		var response = await client.GetAsync($"/pedidos/{pedidoId}");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var details = await response.Content.ReadApiJsonAsync<PedidoDetailsResponse>();
		Assert.NotNull(details);
		return details;
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

	private sealed record EstoqueResponse(int Disponivel, int Reservado);

	private sealed record PedidoCreatedResponse(Guid Id, PedidoStatusResponse Status, decimal Total);

	private sealed record PedidoDetailsResponse(
		Guid Id,
		PedidoStatusResponse Status,
		decimal Total,
		DateTimeOffset CriadoEm,
		PedidoItemDetailsResponse[] Itens);

	private sealed record PedidoItemDetailsResponse(
		Guid ProdutoId,
		string ProdutoNome,
		string ProdutoSku,
		int Quantidade,
		decimal PrecoUnitario,
		decimal Total);

	private enum PedidoStatusResponse
	{
		Aberto = 1,
		Cancelado = 2
	}
}
