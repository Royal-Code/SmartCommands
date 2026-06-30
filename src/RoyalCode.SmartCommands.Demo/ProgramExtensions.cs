using Microsoft.AspNetCore.Mvc;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartCommands.WorkContext.Extensions;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.SmartProblems.HttpResults;
using RoyalCode.SmartSearch;
using RoyalCode.SmartSearch.AspNetCore.HttpResults;
using RoyalCode.SmartSearch.AspNetCore.Internals;
using RoyalCode.SmartSearch.Defaults;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo;

[MapApiHandlers, AddHandlersServices("")]
public static partial class ProgramExtensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddHandlersServices<IWorkContext>();
        builder.Services.AddConcurrencyRetryProblem<EditarProduto>(
            "demo.produtos.editar",
            static (_, _) => Problems.InvalidState(
                "O produto foi alterado por outro processo.",
                typeId: "demo.concurrency_conflict"));
        builder.Services.AddTransient<SomeService>();

        builder.Services.AddWorkContext<CineDbContext>()
            .AddUnitOfWorkAccessor()
            .ConfigureDbContext()
            .ConfigureRepositories(repos =>
            {
                repos.Add<Produto>();
                repos.Add<Loja>();
            })
            .ConfigureSearches(searches =>
            {
                searches.Add<Produto>();
                searches.Add<Loja>();
            });
    }

    public static void ConfigurePipeline(this WebApplication app)
    {
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        var produtosGroup = app.MapProdutosGroup().WithTags("Produtos");
        var lojasGroup = app.MapLojasGroup().WithTags("Lojas");


        // como seria um find
        produtosGroup.MapGet("manual/{id:guid}", FindProdutoAsync);


        // com seria um search
        produtosGroup.MapGet("search/manual", SearchProdutoProdutoFiltroAsync);
        produtosGroup.MapSearch<Produto, ProdutoDetalhes, ProdutoFiltro>("search/auto");

        var x = Exemplo_SearchProdutoAsync;
    }

    [ProduceProblems(ProblemCategory.InvalidParameter, ProblemCategory.InternalServerError)]
    private static Task<MatchSearch<ProdutoDetalhes>> SearchProdutoProdutoFiltroAsync(
        [AsParameters] ProdutoFiltro filter,
        [AsParameters] SearchOptions options,
        [FromQuery] Sorting[]? orderby,
        [FromServices] ICriteria<Produto> criteria,
        [FromServices] ILogger<ICriteria<Produto>> logger,
        CancellationToken ct)
    {
        Action<ICriteria<Produto>>? configure = null;

        return Performer.SearchAsync<Produto, ProdutoDetalhes, ProdutoFiltro>(
            filter, options, orderby, criteria, configure, logger, ct);
    }

    [ProduceProblems(ProblemCategory.InvalidParameter, ProblemCategory.InternalServerError)]
    private static Task<MatchSearch<ProdutoDetalhes>> Exemplo_SearchProdutoAsync(
        [AsParameters] ExemploProdutoFiltro filter,
        [AsParameters] SearchOptions options,
        [FromQuery] Sorting[]? orderby,
        [FromServices] ICriteria<Produto> criteria,
        [FromServices] ILogger<ICriteria<Produto>> logger,
        [FromServices] SomeService some,
        [FromRoute] int id,
        HttpContext context,
        CancellationToken ct)
    {
        Action<ICriteria<Produto>>? configure = (criteria) =>
        {
            filter.ConfigureSearch(criteria, context, some, id);
        };

        Func<ICriteria<Produto>, Task>? conf = async (criteria) =>
        {
            await filter.AnotherMethod();
        };

        return Performer.SearchAsync<Produto, ProdutoDetalhes, ExemploProdutoFiltro>(
            filter, options, orderby, criteria, configure, logger, ct);
    }

    [ProduceProblems(ProblemCategory.NotFound)]
    private static async Task<OkMatch<ProdutoDetalhes>> FindProdutoAsync(
        [FromRoute] Id<Produto, Guid> id, 
        [FromServices] IRepositoryAccessor<Produto> accessor,
        CancellationToken ct)
    {
        var findResult = await accessor.FindEntityAsync<ProdutoDetalhes, Guid>(id, ct);
        if (findResult.NotFound(out var notfoundProblem))
            return notfoundProblem;
        return findResult.Entity;
    }
}
