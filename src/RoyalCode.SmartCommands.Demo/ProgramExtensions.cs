using Microsoft.AspNetCore.Mvc;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartCommands.WorkContext.Extensions;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.SmartProblems.HttpResults;

namespace RoyalCode.SmartCommands.Demo;

[MapApiHandlers, AddHandlersServices("")]
public static partial class ProgramExtensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddHandlersServices();

        builder.Services.AddWorkContext<CineDbContext>()
            .AddUnitOfWorkAdapter()
            .ConfigureDbContext()
            .ConfigureRepositories(repos =>
            {
                repos.Add<Produto>();
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
