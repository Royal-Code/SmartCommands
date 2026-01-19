using Microsoft.EntityFrameworkCore;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Infra.Data.Mappings;

public static class MappingsExtensions
{
    public static void MapSeguranca(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MappingsExtensions).Assembly);
    }
}
