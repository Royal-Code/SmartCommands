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

    public DbSet<ProdutoEstoque> Estoques { get; set; } = null!;

    public DbSet<Pedido> Pedidos { get; set; } = null!;

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

        var estoque = modelBuilder.Entity<ProdutoEstoque>();
        estoque.HasKey(e => e.Id);
        estoque.Property(e => e.ProdutoId).IsRequired();
        estoque.HasIndex(e => e.ProdutoId).IsUnique();
        estoque.Property(e => e.Disponivel).IsRequired();
        estoque.Property(e => e.Reservado).IsRequired();
        estoque.Property(e => e.Version).IsConcurrencyToken();
        estoque.HasOne(e => e.Produto)
            .WithOne()
            .HasForeignKey<ProdutoEstoque>(e => e.ProdutoId)
            .OnDelete(DeleteBehavior.Cascade);

        var pedido = modelBuilder.Entity<Pedido>();
        pedido.HasKey(p => p.Id);
        pedido.Property(p => p.Status).IsRequired();
        pedido.Property(p => p.Total).IsRequired();
        pedido.Property(p => p.CriadoEm).IsRequired();
        pedido.HasMany(p => p.Itens)
            .WithOne()
            .HasForeignKey(i => i.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);

        var pedidoItem = modelBuilder.Entity<PedidoItem>();
        pedidoItem.HasKey(i => i.Id);
        pedidoItem.Property(i => i.ProdutoId).IsRequired();
        pedidoItem.Property(i => i.ProdutoNome).IsRequired();
        pedidoItem.Property(i => i.ProdutoSku).IsRequired();
        pedidoItem.Property(i => i.Quantidade).IsRequired();
        pedidoItem.Property(i => i.PrecoUnitario).IsRequired();
        pedidoItem.Property(i => i.Total).IsRequired();

        var loja = modelBuilder.Entity<Loja>();
        loja.HasKey(l => l.Id);
        loja.Property(l => l.Nome).IsRequired();
        loja.Property(l => l.Endereco).IsRequired();
    }
}
