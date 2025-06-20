using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartCommands.WorkContext.Extensions;

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
        app.MapGroup("api").MapGroup("entity").MapGet("{id}", FindEntityAsync);
    }

    private static async Task<IResult> FindEntityAsync(int id, ICineDbContext context)
    {
        var entity = await context.FindAsync<Produto>(id);
        return entity is not null ? Results.Ok(entity) : Results.NotFound();
    }
}
