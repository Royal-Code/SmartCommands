using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.Demo.Tests.Support;
using RoyalCode.SmartCommands.WorkContext.Extensions;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo.Tests;

public class DemoApiConcurrencyRetryTests
{
	[Fact]
	public async Task EditProduto_Must_Retry_And_Succeed_When_ConcurrencyConflictIsTransient()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var created = await CreateProductAsync(client, "Produto A");

		var response = await client.PutAsJsonAsync($"/produtos/{created.Id}", new
		{
			Nome = ConcurrencyFailureController.RetryOnceProductName,
			Preco = 20m
		});

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(1, app.ConcurrencyFailures.Failures);

		var findResponse = await client.GetAsync($"/produtos/{created.Id}");
		var found = await findResponse.Content.ReadApiJsonAsync<ProdutoDetails>();
		Assert.NotNull(found);
		Assert.Equal(ConcurrencyFailureController.RetryOnceProductName, found.Nome);
	}

	[Fact]
	public async Task EditProduto_Must_Use_DemoProblemFactory_When_RetryIsExhausted()
	{
		await AssertRetryExhaustedProblemAsync(
			null,
			3,
			"O produto foi alterado por outro processo.");
	}

	[Fact]
	public async Task EditProduto_Must_Use_TypedDelegateProblemFactory_When_RetryIsExhausted()
	{
		await AssertRetryExhaustedProblemAsync(
			DemoApiFactory.UseTypedRetryProblemDelegate,
			2,
			"typed",
			ConcurrencyFailureController.RetryAlwaysProductName,
			"demo.produtos.editar");
	}

	[Fact]
	public async Task EditProduto_Must_Use_ServiceDelegateProblemFactory_When_RetryIsExhausted()
	{
		await AssertRetryExhaustedProblemAsync(
			DemoApiFactory.UseServiceRetryProblemDelegate,
			2,
			"service",
			ConcurrencyFailureController.RetryAlwaysProductName,
			"demo.produtos.editar");
	}

	[Fact]
	public async Task EditProduto_Must_Use_ProviderProblemFactory_When_RetryIsExhausted()
	{
		await AssertRetryExhaustedProblemAsync(
			DemoApiFactory.UseProviderRetryProblem,
			2,
			"provider",
			ConcurrencyFailureController.RetryAlwaysProductName,
			"demo.produtos.editar");
	}

	[Fact]
	public async Task EditProduto_Must_Use_ConfiguredFallbackProblem_When_NoOperationRegistrationExists()
	{
		await AssertRetryExhaustedProblemAsync(
			DemoApiFactory.UseFallbackRetryProblem,
			2,
			"fallback concurrency conflict");
	}

	[Fact]
	public async Task EditProduto_Must_Retry_AndSucceed_When_SaveAsyncConvertsDbUpdateConcurrencyException()
	{
		// F2: o gatilho lança a DbUpdateConcurrencyException crua do EF; só há retry se UnitOfWork.SaveAsync
		// a converter para ConcurrencyException. Este teste cobre esse caminho de conversão ponta a ponta.
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var created = await CreateProductAsync(client, "Produto A");

		var response = await client.PutAsJsonAsync($"/produtos/{created.Id}", new
		{
			Nome = ConcurrencyFailureController.RetryOnceWithDbUpdateProductName,
			Preco = 20m
		});

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(1, app.ConcurrencyFailures.Failures);

		var findResponse = await client.GetAsync($"/produtos/{created.Id}");
		var found = await findResponse.Content.ReadApiJsonAsync<ProdutoDetails>();
		Assert.NotNull(found);
		Assert.Equal(ConcurrencyFailureController.RetryOnceWithDbUpdateProductName, found.Nome);
	}

	[Fact]
	public async Task DesativarProduto_WithoutOperation_Must_UseConfiguredProblemOptions_OnExhaustion()
	{
		// Fase 8: mesmo sem Operation no atributo, o handler gerado injeta a factory e usa a chave
		// default ({namespace}.{Comando}); as options ExhaustedProblemDetail/ExhaustedProblemTypeId
		// valem em todos os caminhos.
		using var app = new DemoApiFactory(static services => services.Configure<RetryOnConcurrencyOptions>(options =>
		{
			options.MaxAttempts = 2;
			options.ExhaustedProblemDetail = "detalhe configurado nas options";
			// typeId configurado precisa estar no catálogo RFC 9457 do Demo para virar o campo "type";
			// usa um typeId já catalogado para validar o caminho completo
			options.ExhaustedProblemTypeId = "demo.concurrency_conflict";
		}));
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var created = await CreateProductAsync(client, ConcurrencyFailureController.RetryAlwaysProductName);

		// DesativarProduto não tem corpo: a requisição é enviada sem body (o endpoint instancia o comando).
		var response = await client.PatchAsync($"/produtos/{created.Id}/desativar", content: null);

		await response.AssertProblemAsync(HttpStatusCode.Conflict, "detalhe configurado nas options", "demo.concurrency_conflict");

		var content = await response.Content.ReadApiTextAsync();
		Assert.DoesNotContain(ConcurrencyRetryExtensions.ConcurrencyConflictDetail, content, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(2, app.ConcurrencyFailures.Failures);
	}

	[Fact]
	public async Task DesativarProduto_WithoutOperation_Must_UseRegistrationByDefaultKey_OnExhaustion()
	{
		// Fase 8: o registro sem operation usa a mesma chave default do handler gerado
		using var app = new DemoApiFactory(static services =>
		{
			services.Configure<RetryOnConcurrencyOptions>(static options => options.MaxAttempts = 2);
			services.AddConcurrencyRetryProblem<Demo.Commands.Produtos.DesativarProduto>(
				static (_, _) => Problems.InvalidState(
					"produto desativado por outro processo",
					typeId: "demo.concurrency_conflict"));
		});
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var created = await CreateProductAsync(client, ConcurrencyFailureController.RetryAlwaysProductName);

		var response = await client.PatchAsync($"/produtos/{created.Id}/desativar", content: null);

		await response.AssertProblemAsync(
			HttpStatusCode.Conflict,
			"produto desativado por outro processo",
			"demo.concurrency_conflict");
		Assert.Equal(2, app.ConcurrencyFailures.Failures);
	}

	private static async Task AssertRetryExhaustedProblemAsync(
		Action<IServiceCollection>? configureServices,
		int expectedFailures,
		params string[] expectedProblemFragments)
	{
		using var app = new DemoApiFactory(configureServices);
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var created = await CreateProductAsync(client, "Produto A");

		var response = await client.PutAsJsonAsync($"/produtos/{created.Id}", new
		{
			Nome = ConcurrencyFailureController.RetryAlwaysProductName,
			Preco = 20m
		});

		await response.AssertProblemAsync(HttpStatusCode.Conflict, expectedProblemFragments);
		Assert.Equal(expectedFailures, app.ConcurrencyFailures.Failures);
	}

	private static async Task<CreateProdutoResponse> CreateProductAsync(HttpClient client, string nome)
	{
		var createResponse = await client.PostAsJsonAsync("/produtos/", new
		{
			Nome = nome,
			Sku = $"SKU-{Guid.NewGuid():N}",
			Preco = 10m
		});

		Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
		var created = await createResponse.Content.ReadApiJsonAsync<CreateProdutoResponse>();
		Assert.NotNull(created);
		return created;
	}

	private sealed record CreateProdutoResponse(Guid Id, string Nome);

	private sealed record ProdutoDetails(Guid Id, string Nome, bool Ativo);
}
