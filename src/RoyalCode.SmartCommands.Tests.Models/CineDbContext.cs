using Microsoft.EntityFrameworkCore;

namespace RoyalCode.SmartCommands.Tests.Models;

public class CineDbContext : DbContext
{
    public CineDbContext() { }

    public CineDbContext(DbContextOptions<CineDbContext> options) : base(options) { }

    public DbSet<Produto> Produtos { get; set; }

    public DbSet<Movie> Movies { get; set; }
    public DbSet<Director> Directors { get; set; }
    public DbSet<Actor> Actors { get; set; }
    public DbSet<Genre> Genres { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseInMemoryDatabase("CineDb");
    }
}