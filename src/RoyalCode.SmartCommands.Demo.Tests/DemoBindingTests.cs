using System.Net;
using System.Net.Http.Json;
using RoyalCode.SmartCommands.Demo.Tests.Support;

namespace RoyalCode.SmartCommands.Demo.Tests;

/// <summary>
/// Fase 5 (DF2/DF3): o delegate Minimal API gerado vincula valores externos de rota, query, header,
/// serviço de DI e tipo com <c>BindAsync</c> customizado, preservando os atributos explícitos do
/// parâmetro-fonte e usando a inferência do ASP.NET Core para os demais.
/// </summary>
public class DemoBindingTests
{
	private sealed record VisualizacaoRegistradaResponse(
		int MovieId,
		string? Plataforma,
		string? Origem,
		string? Usuario,
		DateTimeOffset Momento,
		DateTimeOffset RegistradoEm);

	[Fact]
	public async Task RegistrarVisualizacao_Must_Bind_Route_Query_Header_BindAsync_And_Service()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var momento = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
		using var request = new HttpRequestMessage(
			HttpMethod.Post,
			$"/playground/42/views?origem=web&momento={Uri.EscapeDataString(momento.ToString("O"))}")
		{
			Content = JsonContent.Create(new { Plataforma = "tv" }),
		};
		request.Headers.Add("x-demo-user", "glauco");

		var before = DateTimeOffset.UtcNow.AddSeconds(-5);
		var response = await client.SendAsync(request);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var registered = await response.Content.ReadApiJsonAsync<VisualizacaoRegistradaResponse>();
		Assert.NotNull(registered);
		Assert.Equal(42, registered.MovieId);                 // rota ([FromRoute(Name = "movieId")])
		Assert.Equal("tv", registered.Plataforma);            // body implícito (command)
		Assert.Equal("web", registered.Origem);               // query ([FromQuery(Name = "origem")])
		Assert.Equal("glauco", registered.Usuario);           // header ([FromHeader(Name = "x-demo-user")])
		Assert.Equal(momento, registered.Momento);            // BindAsync customizado (query 'momento')
		Assert.True(registered.RegistradoEm >= before);       // serviço de DI inferido (IRelogioDemo)
	}

	[Fact]
	public async Task RegistrarVisualizacao_Validation_Rejects_Unknown_Platform()
	{
		// Fase 6 (DF13): a validação adicional assíncrona (serviço IPlataformasPermitidas) roda antes da
		// execução do comando e interrompe o handler com um problema
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var response = await client.PostAsJsonAsync("/playground/9/views", new { Plataforma = "vhs" });

		await response.AssertProblemAsync(HttpStatusCode.BadRequest, "vhs", "não é aceita");
	}

	[Fact]
	public async Task RegistrarVisualizacao_Optional_Sources_Are_Null_When_Absent()
	{
		using var app = new DemoApiFactory();
		using var client = app.CreateClient();
		await app.ResetDatabaseAsync();

		var before = DateTimeOffset.UtcNow.AddSeconds(-5);
		var response = await client.PostAsJsonAsync("/playground/7/views", new { Plataforma = (string?)null });

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var registered = await response.Content.ReadApiJsonAsync<VisualizacaoRegistradaResponse>();
		Assert.NotNull(registered);
		Assert.Equal(7, registered.MovieId);
		Assert.Null(registered.Plataforma);
		Assert.Null(registered.Origem);
		Assert.Null(registered.Usuario);
		Assert.True(registered.Momento >= before);            // BindAsync sem query usa o horário atual
	}
}
