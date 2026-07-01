using System.Net;
using System.Net.Http.Json;
using RoyalCode.SmartCommands.Demo.Tests.Support;

namespace RoyalCode.SmartCommands.Demo.Tests;

// Fase 4 do plan-demo-usage-scenarios: busca avancada de produtos sobre o endpoint /produtos (SmartSearch).
// Exercita filtros compostos (nome parcial, faixa de preco, disponibilidade em estoque via caminho aninhado)
// e paginacao. A ordenacao HTTP (?orderby) esta bloqueada por gaps do SmartSearch (ver "Registro de gaps" no
// plano) e aqui e apenas caracterizada (nao funciona hoje), nao afirmada como funcional.
public class BuscaProdutosTests
{
	[Fact]
	public async Task FiltrarPorNomeParcial_RetornaApenasCorrespondentes()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		await CreateProductAsync(client, "Camiseta Azul", "CAM-001", 40m);
		await CreateProductAsync(client, "Camiseta Preta", "CAM-002", 45m);
		await CreateProductAsync(client, "Bermuda Jeans", "BER-001", 90m);

		var (status, page) = await SearchAsync(client, "?nome=Camiseta");

		Assert.Equal(HttpStatusCode.OK, status);
		Assert.NotNull(page);
		Assert.Equal(2, page.Items.Length);
		Assert.All(page.Items, p => Assert.Contains("Camiseta", p.Nome));
	}

	[Fact]
	public async Task FiltrarPorFaixaDePreco_RespeitaLimites()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		await CreateProductAsync(client, "Barato", "PRC-010", 10m);
		await CreateProductAsync(client, "Medio", "PRC-050", 50m);
		await CreateProductAsync(client, "Caro", "PRC-100", 100m);

		var (status, page) = await SearchAsync(client, "?precoMinimo=20&precoMaximo=80");

		Assert.Equal(HttpStatusCode.OK, status);
		Assert.NotNull(page);
		var item = Assert.Single(page.Items);
		Assert.Equal(50m, item.Preco);
	}

	[Fact]
	public async Task FaixaDePreco_ForaDosLimites_Retorna204()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		await CreateProductAsync(client, "Barato", "PRC-010", 10m);
		await CreateProductAsync(client, "Medio", "PRC-050", 50m);

		// SmartSearch devolve 204 (NoContent) quando o resultado e vazio.
		var (status, _) = await SearchAsync(client, "?precoMinimo=1000");

		Assert.Equal(HttpStatusCode.NoContent, status);
	}

	[Fact]
	public async Task Paginacao_RetornaTotalEItensDaPagina()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		await CreateProductAsync(client, "A", "PAG-001", 10m);
		await CreateProductAsync(client, "B", "PAG-002", 20m);
		await CreateProductAsync(client, "C", "PAG-003", 30m);

		var (firstStatus, first) = await SearchAsync(client, "?itemsPerPage=2&page=1");
		Assert.Equal(HttpStatusCode.OK, firstStatus);
		Assert.NotNull(first);
		Assert.Equal(3, first.Count);           // total de registros
		Assert.Equal(2, first.ItemsPerPage);
		Assert.Equal(2, first.Items.Length);    // primeira pagina cheia

		var (secondStatus, second) = await SearchAsync(client, "?itemsPerPage=2&page=2");
		Assert.Equal(HttpStatusCode.OK, secondStatus);
		Assert.NotNull(second);
		Assert.Equal(3, second.Count);
		Assert.Single(second.Items);            // segunda pagina com o restante
	}

	[Fact]
	public async Task BuscaPorDisponibilidade_ConsideraEstoqueReservado()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		// P1: estoque totalmente reservado -> Disponivel 0 (nao deve aparecer).
		var semDisponivel = await CreateProductAsync(client, "Sem disponivel", "DSP-001", 15m);
		await RegistrarEstoqueAsync(client, semDisponivel, 10);
		await ReservarAsync(client, semDisponivel, 10);

		// P2: estoque com saldo disponivel -> deve aparecer.
		var comDisponivel = await CreateProductAsync(client, "Com disponivel", "DSP-002", 25m);
		await RegistrarEstoqueAsync(client, comDisponivel, 5);

		// P3: sem estoque registrado -> LEFT JOIN nulo (nao deve aparecer).
		await CreateProductAsync(client, "Sem estoque", "DSP-003", 35m);

		var (status, page) = await SearchAsync(client, "?estoqueDisponivelMinimo=1");

		Assert.Equal(HttpStatusCode.OK, status);
		Assert.NotNull(page);
		var item = Assert.Single(page.Items);
		Assert.Equal("DSP-002", item.Sku);
	}

	// --- Caracterizacao de gaps do SmartSearch (comportamento atual, nao o desejado) ---

	[Fact]
	public async Task Ordenacao_PorPropriedadeValida_AtualmenteNaoTemEfeito_GapConhecido()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		await CreateProductAsync(client, "Gama", "ORD-003", 30m);
		await CreateProductAsync(client, "Alfa", "ORD-001", 10m);
		await CreateProductAsync(client, "Beta", "ORD-002", 20m);

		var (semOrdemStatus, semOrdem) = await SearchAsync(client, "");
		var (comOrdemStatus, comOrdem) = await SearchAsync(client, "?orderby=Nome");

		Assert.Equal(HttpStatusCode.OK, semOrdemStatus);
		Assert.Equal(HttpStatusCode.OK, comOrdemStatus);
		Assert.NotNull(semOrdem);
		Assert.NotNull(comOrdem);

		// GAP (SmartSearch): ?orderby por propriedade valida e ignorado; a busca cai na ordenacao default (Id).
		// Portanto a sequencia com e sem orderby e identica. Quando o gap for corrigido na lib, a ordenacao
		// passara a valer e este teste falhara, sinalizando para promover isto a um teste de ordenacao real.
		Assert.Equal(
			semOrdem.Items.Select(i => i.Sku).ToArray(),
			comOrdem.Items.Select(i => i.Sku).ToArray());
	}

	[Fact]
	public async Task OrderByInvalido_AtualmenteRetornaErro_GapConhecido()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		await CreateProductAsync(client, "Alfa", "INV-001", 10m);

		var response = await client.GetAsync("/produtos?orderby=CampoInexistente");

		// GAP (SmartSearch): orderby invalido lanca OrderByNotSupportedException (ArgumentException), que o
		// Performer nao captura (so trata OrderByException), entao vaza como 500 InternalError. O esperado
		// seria 400 InvalidParameter. Caracterizamos >= 400 para ser robusto a correcao futura para 400.
		Assert.True((int)response.StatusCode >= 400, $"esperava erro, veio {(int)response.StatusCode}");
	}

	private static async Task<Guid> CreateProductAsync(HttpClient client, string nome, string sku, decimal preco)
	{
		var create = await client.PostAsJsonAsync("/produtos/", new { Nome = nome, Sku = sku, Preco = preco });
		Assert.Equal(HttpStatusCode.Created, create.StatusCode);
		var created = await create.Content.ReadApiJsonAsync<ProdutoResponse>();
		Assert.NotNull(created);
		return created.Id;
	}

	private static async Task RegistrarEstoqueAsync(HttpClient client, Guid produtoId, int quantidade)
	{
		var response = await client.PostAsJsonAsync($"/produtos/{produtoId}/estoque", new { Quantidade = quantidade });
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	private static async Task ReservarAsync(HttpClient client, Guid produtoId, int quantidade)
	{
		var response = await client.PostAsJsonAsync($"/produtos/{produtoId}/estoque/reservas", new { Quantidade = quantidade });
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	private static async Task<(HttpStatusCode Status, PagedProdutos? Page)> SearchAsync(HttpClient client, string query)
	{
		var response = await client.GetAsync($"/produtos{query}");

		if (response.StatusCode != HttpStatusCode.OK)
			return (response.StatusCode, null);

		var page = await response.Content.ReadApiJsonAsync<PagedProdutos>();
		return (response.StatusCode, page);
	}

	private sealed record ProdutoResponse(Guid Id);

	private sealed record ProdutoItem(Guid Id, string Nome, string Sku, decimal Preco, bool Ativo);

	private sealed record PagedProdutos(int Count, int Page, int ItemsPerPage, int Pages, ProdutoItem[] Items);
}
