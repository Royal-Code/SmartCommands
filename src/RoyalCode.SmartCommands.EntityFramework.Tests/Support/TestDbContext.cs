using Microsoft.EntityFrameworkCore;

namespace RoyalCode.SmartCommands.EntityFramework.Tests.Support;

/// <summary>
/// Entidade usada nos testes do adapter EF; <see cref="Versao"/> é o token de concorrência otimista.
/// </summary>
public class Gadget
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public int Versao { get; set; }
}

/// <summary>
/// DTO de projeção usado pelos testes do <c>RepositoryAdapter</c>.
/// </summary>
public class GadgetNome
{
    public string Nome { get; set; } = string.Empty;
}

/// <summary>
/// Projeção que contém a entidade para comprovar que o adapter aplica no-tracking mesmo quando o
/// seletor inclui uma instância de tipo entidade no resultado.
/// </summary>
public class GadgetEnvelope
{
    public Gadget Entity { get; set; } = null!;
}

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<Gadget> Gadgets => Set<Gadget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Gadget>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Nome).IsRequired();
            entity.Property(g => g.Versao).IsConcurrencyToken();
            entity.HasQueryFilter(g => g.Nome != "Hidden");
        });
    }
}
