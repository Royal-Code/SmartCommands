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

        app.MapGroup("api").MapProdutosGroup();
        app.MapGroup("api").MapLojasGroup();


        // como seria um find
        app.MapGroup("api").MapGroup("entity").MapGet("{id}", FindProdutoAsync);
    }

    [ProduceProblems(ProblemCategory.NotFound)]
    private static async Task<OkMatch<Produto>> FindProdutoAsync(
        Id<Produto, int> id, 
        IRepositoriesAccessor<Produto> accessor, 
        CancellationToken ct)
    {
        var findResult = await accessor.FindEntityAsync(id, ct);
        if (findResult.NotFound(out var notfoundProblem))
            return notfoundProblem;
        return findResult.Entity;
    }
}
