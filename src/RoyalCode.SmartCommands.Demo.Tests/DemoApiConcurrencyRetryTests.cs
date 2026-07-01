using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.Demo.Tests.Support;
using RoyalCode.SmartCommands.WorkContext.Options;
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
			Nome = ConcurrencyFailureController.RetryOnceProductName
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
			Nome = ConcurrencyFailureController.RetryOnceWithDbUpdateProductName
		});

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(1, app.ConcurrencyFailures.Failures);

		var findResponse = await client.GetAsync($"/produtos/{created.Id}");
		var found = await findResponse.Content.ReadApiJsonAsync<ProdutoDetails>();
		Assert.NotNull(found);
		Assert.Equal(ConcurrencyFailureController.RetryOnceWithDbUpdateProductName, found.Nome);
	}

	[Fact]
	public async Task DesativarProduto_WithoutOperation_Must_IgnoreConfiguredProblemOptions_OnExhaustion()
	{
		// F1: o comando DesativarProduto não declara Operation, então o handler gerado não injeta a
		// IConcurrencyRetryProblemFactory e usa o problema genérico da primitiva — ignorando as options
		// ExhaustedProblemDetail/ExhaustedProblemTypeId. Este teste fixa esse comportamento.
		using var app = new DemoApiFactory(static services => services.Configure<RetryOnConcurrencyOptions>(options =>
		{
			options.MaxAttempts = 2;
			options.ExhaustedProblemDetail = "detalhe das options nao deve aparecer";
			options.ExhaustedProblemTypeId = "demo.should_not_apply";
		}));
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var created = await CreateProductAsync(client, ConcurrencyFailureController.RetryAlwaysProductName);

		// DesativarProduto não tem corpo: a requisição é enviada sem body (o endpoint instancia o comando).
		var response = await client.PatchAsync($"/produtos/{created.Id}/desativar", content: null);

		await response.AssertProblemAsync(HttpStatusCode.Conflict, ConcurrencyRetryExtensions.ConcurrencyConflictDetail);

		var content = await response.Content.ReadApiTextAsync();
		Assert.DoesNotContain("detalhe das options nao deve aparecer", content, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("demo.should_not_apply", content, StringComparison.OrdinalIgnoreCase);
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
			Nome = ConcurrencyFailureController.RetryAlwaysProductName
		});

		await response.AssertProblemAsync(HttpStatusCode.Conflict, expectedProblemFragments);
		Assert.Equal(expectedFailures, app.ConcurrencyFailures.Failures);
	}

	private static async Task<CreateProdutoResponse> CreateProductAsync(HttpClient client, string nome)
	{
		var createResponse = await client.PostAsJsonAsync("/produtos/", new { Nome = nome });

		Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
		var created = await createResponse.Content.ReadApiJsonAsync<CreateProdutoResponse>();
		Assert.NotNull(created);
		return created;
	}

	private sealed record CreateProdutoResponse(Guid Id, string Nome);

	private sealed record ProdutoDetails(Guid Id, string Nome, bool Ativo);
}
