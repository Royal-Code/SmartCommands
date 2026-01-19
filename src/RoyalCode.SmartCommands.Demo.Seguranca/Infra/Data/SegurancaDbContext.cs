using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.Demo.Seguranca.Infra.Data.Mappings;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Infra.Data;

public class SegurancaDbContext : DbContext
{
    public SegurancaDbContext(DbContextOptions<SegurancaDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.MapSeguranca();

        base.OnModelCreating(modelBuilder);
    }
}
