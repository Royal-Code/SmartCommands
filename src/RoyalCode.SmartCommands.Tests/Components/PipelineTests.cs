using Coreum.NewCommands.Tests.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartProblems;
using System.Text;

namespace Coreum.NewCommands.Tests.Components;

public class PipelineTests
{
    [Fact]
    public async Task Pipeline_Must_ExecuteAllDecorators_And_CreateTheEntity()
    {
        ServiceCollection services = [];
        services.AddWorkContext<TestDbContext>()
            .ConfigureDbContext(builder => builder.UseInMemoryDatabase(nameof(Pipeline_Must_ExecuteAllDecorators_And_CreateTheEntity)))
            .ConfigureRepositories(builder => builder.Add<Produto>());

        services.AddTransient<PipelineCriarProdutoHandler>();
        services.AddSingleton<TestLogger>();
        services.AddTransient<IDecorator<CriarProduto, Result<Guid>>, Decorator1>();
        services.AddTransient<IDecorator<CriarProduto, Result<Guid>>, Decorator2>();
        services.AddTransient<IDecorator<CriarProduto, Result<Guid>>, Decorator3>();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var pipe = scope.ServiceProvider.GetRequiredService<PipelineCriarProdutoHandler>();

        var model = new CriarProduto()
        {
            Nome = "Testes"
        };

        var result = await pipe.HandleAsync(model, default);
        var hasProblems = result.HasProblems(out var problems);

        Assert.False(hasProblems);

        using var assertScope = sp.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<TestDbContext>();
        var logger = assertScope.ServiceProvider.GetRequiredService<TestLogger>();

        var hasProduto = await db.Set<Produto>().AnyAsync(p => p.Nome == "Testes");
        Assert.True(hasProduto);

        var expectedLogs = new StringBuilder()
            .AppendLine("Inicio Decorator 1")
            .AppendLine("Inicio Decorator 2")
            .AppendLine("Inicio Decorator 3")
            .AppendLine("Fim Decorator 3")
            .AppendLine("Fim Decorator 2")
            .AppendLine("Fim Decorator 1")
            .ToString();
            
        Assert.Equal(expectedLogs, logger.ToString());
    }
}

file class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Produto>();
        //modelBuilder.Entity<Variacao>();
        //modelBuilder.Entity<Cor>();
        //modelBuilder.Entity<Tamanho>();

        base.OnModelCreating(modelBuilder);
    }
}

file class TestLogger
{
    private readonly StringBuilder builder;

    public TestLogger()
    {
        builder = new();
    }

    public void Log(string message)
    {
        builder.AppendLine(message);
    }

    public override string ToString()
    {
        return builder.ToString();
    }
}

file class Decorator1 : IDecorator<CriarProduto, Result<Guid>>
{
    private readonly TestLogger logger;

    public Decorator1(TestLogger logger)
    {
        this.logger = logger;
    }

    public async Task<Result<Guid>> HandleAsync(CriarProduto command, Func<Task<Result<Guid>>> next, CancellationToken ct)
    {
        logger.Log("Inicio Decorator 1");

        var result = await next();

        logger.Log("Fim Decorator 1");

        return result;
    }
}

file class Decorator2 : IDecorator<CriarProduto, Result<Guid>>
{
    private readonly TestLogger logger;

    public Decorator2(TestLogger logger)
    {
        this.logger = logger;
    }

    public async Task<Result<Guid>> HandleAsync(CriarProduto command, Func<Task<Result<Guid>>> next, CancellationToken ct)
    {
        logger.Log("Inicio Decorator 2");

        var result = await next();

        logger.Log("Fim Decorator 2");

        return result;
    }
}

file class Decorator3 : IDecorator<CriarProduto, Result<Guid>>
{
    private readonly TestLogger logger;

    public Decorator3(TestLogger logger)
    {
        this.logger = logger;
    }

    public async Task<Result<Guid>> HandleAsync(CriarProduto command, Func<Task<Result<Guid>>> next, CancellationToken ct)
    {
        logger.Log("Inicio Decorator 3");

        var result = await next();

        logger.Log("Fim Decorator 3");

        return result;
    }
}