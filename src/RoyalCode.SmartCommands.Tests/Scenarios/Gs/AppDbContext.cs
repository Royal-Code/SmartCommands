using Microsoft.EntityFrameworkCore;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Gs;

public class AppDbContext : DbContext
{
    public DbSet<Some> Some { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase("AppDbContextGs");
    }
}