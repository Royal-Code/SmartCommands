using Microsoft.EntityFrameworkCore;

namespace RoyalCode.SmartCommands.Demo.Domain;

/// <summary>
/// DbContext proprio da demo. Substitui o `CineDbContext` de Tests.Models (ver Decisoes iniciais do plano).
/// </summary>
public class DemoDbContext : DbContext
{
    public DemoDbContext() { }

    public DemoDbContext(DbContextOptions<DemoDbContext> options) : base(options) { }

    public DbSet<Produto> Produtos { get; set; } = null!;

    public DbSet<Loja> Lojas { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseInMemoryDatabase("DemoDb");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var produto = modelBuilder.Entity<Produto>();
        produto.HasKey(p => p.Id);
        produto.Property(p => p.Nome).IsRequired();
        produto.Property(p => p.Sku).IsRequired();
        // SKU unico no catalogo (defesa em profundidade; o comando tambem valida antes de gravar).
        produto.HasIndex(p => p.Sku).IsUnique();

        var loja = modelBuilder.Entity<Loja>();
        loja.HasKey(l => l.Id);
        loja.Property(l => l.Nome).IsRequired();
        loja.Property(l => l.Endereco).IsRequired();
    }
}
