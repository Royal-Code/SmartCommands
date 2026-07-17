using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoyalCode.SmartCommands.EntityFramework.Extensions;
using RoyalCode.SmartCommands.EntityFramework.Options;
using RoyalCode.SmartCommands.EntityFramework.Tests.Commands;
using RoyalCode.SmartCommands.EntityFramework.Tests.Support;

namespace RoyalCode.SmartCommands.EntityFramework.Tests;

/// <summary>
/// Borda HTTP (DF14 dentro de HTTP): o adapter permanece agnóstico de HTTP; problemas conhecidos
/// fluem pelo <c>Result</c> do handler (409), e exceções inesperadas atravessam até o
/// <c>UseExceptionHandler</c> + <c>ProblemDetails</c> do app (500), sem duplicar tratamento
/// dentro do adapter e sem vazar detalhe do provider na resposta.
/// </summary>
public class HttpBoundaryTests
{
    private static async Task<WebApplication> StartAppAsync(SqliteDatabase database)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddProblemDetails();
        builder.Services.AddScoped(_ => database.CreateContext());
        builder.Services.AddUnitOfWorkAccessor<TestDbContext>();
        builder.Services.Configure<DbContextAdapterOptions>(o => o.BeginTransactions = true);
        builder.Services.AddGadgetHandlersServices();

        var app = builder.Build();

        // o tratamento de exceção pertence à borda do app, não ao adapter
        app.UseExceptionHandler();

        app.MapGadgetsGroup();

        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Sucesso_retorna_201_e_persiste()
    {
        using var database = new SqliteDatabase();
        await using var app = await StartAppAsync(database);
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/gadgets", new { nome = "g1" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, database.CountGadgets());
    }

    [Fact]
    public async Task Excecao_inesperada_vira_problemdetails_500_na_borda_do_app()
    {
        using var database = new SqliteDatabase();
        await using var app = await StartAppAsync(database);
        var client = app.GetTestClient();

        database.SaveInterceptor.Throw = new InvalidOperationException("detalhe interno sensivel");

        var response = await client.PostAsJsonAsync("/gadgets", new { nome = "g1" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        // a mensagem da exceção de persistência não vaza na resposta HTTP
        Assert.DoesNotContain("detalhe interno sensivel", body);
    }

    [Fact]
    public async Task Conflito_de_concorrencia_vira_409_pelo_result_sem_middleware_de_excecao()
    {
        using var database = new SqliteDatabase();
        await using var app = await StartAppAsync(database);
        var client = app.GetTestClient();

        database.SaveInterceptor.Throw = new DbUpdateConcurrencyException("detalhe do provider");

        var response = await client.PostAsJsonAsync("/gadgets", new { nome = "g1" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        // o problema conhecido usa o detalhe genérico; o texto do provider não vaza
        Assert.DoesNotContain("detalhe do provider", body);
        Assert.Contains("modified by another process", body);
    }
}
