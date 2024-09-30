using Microsoft.EntityFrameworkCore;

namespace Coreum.NewCommands.Tests.Scenarios.Fs;

public class AppDbContext : DbContext
{
    public DbSet<Some> Some { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase("AppDbContextFs");
    }
}