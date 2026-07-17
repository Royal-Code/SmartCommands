using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.EntityFramework.Adapters;
using RoyalCode.SmartCommands.EntityFramework.Options;

namespace RoyalCode.SmartCommands.EntityFramework.Tests.Support;

/// <summary>
/// Banco SQLite in-memory compartilhado entre contextos pelo tempo de vida da conexão aberta,
/// com os interceptors de falha registrados em todos os contextos criados.
/// </summary>
public sealed class SqliteDatabase : IDisposable
{
    public SqliteConnection Connection { get; }

    public SaveFailureInterceptor SaveInterceptor { get; } = new();

    public TransactionFailureInterceptor TransactionInterceptor { get; } = new();

    public SqliteDatabase()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        using var db = CreateContext();
        db.Database.EnsureCreated();

        TransactionInterceptor.ResetCounters();
    }

    public TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(Connection)
            .AddInterceptors(SaveInterceptor, TransactionInterceptor)
            .Options;

        return new TestDbContext(options);
    }

    public DbContextAccessor<TestDbContext> CreateAccessor(TestDbContext db, bool beginTransactions)
    {
        var options = new DbContextAdapterOptions { BeginTransactions = beginTransactions };
        return new DbContextAccessor<TestDbContext>(db, Microsoft.Extensions.Options.Options.Create(options));
    }

    /// <summary>Insere um gadget por um contexto próprio e retorna o id.</summary>
    public Guid SeedGadget(string nome)
    {
        using var db = CreateContext();
        var gadget = new Gadget { Id = Guid.NewGuid(), Nome = nome };
        db.Gadgets.Add(gadget);
        db.SaveChanges();

        TransactionInterceptor.ResetCounters();
        return gadget.Id;
    }

    /// <summary>Conta os gadgets persistidos usando um contexto novo (estado real do banco).</summary>
    public int CountGadgets()
    {
        using var db = CreateContext();
        return db.Gadgets.Count();
    }

    /// <summary>Atualização rival: incrementa a versão do gadget por outro contexto (conflito otimista).</summary>
    public void UpdateGadgetByRival(Guid id)
    {
        using var db = CreateContext();
        var gadget = db.Gadgets.Single(g => g.Id == id);
        gadget.Nome = gadget.Nome + " (rival)";
        gadget.Versao++;
        db.SaveChanges();
    }

    public void Dispose() => Connection.Dispose();
}
