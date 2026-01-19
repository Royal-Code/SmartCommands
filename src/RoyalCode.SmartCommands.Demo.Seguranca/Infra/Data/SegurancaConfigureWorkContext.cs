using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.Demo.Seguranca.Infra.Data.Mappings;
using RoyalCode.WorkContext.EntityFramework.Configurations;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Infra.Data;

public static class SegurancaConfigureWorkContext
{
    public static IWorkContextBuilder<TDbContext> ConfigureSeguranca<TDbContext>(this IWorkContextBuilder<TDbContext> builder)
        where TDbContext : DbContext
    {
        return builder.ConfigureModel(modelBuilder => modelBuilder.MapSeguranca())
            .AddRepositories(typeof(SegurancaConfigureWorkContext).Assembly)
            .ConfigureSearches(typeof(SegurancaConfigureWorkContext).Assembly)
            .ConfigureCommands(typeof(SegurancaConfigureWorkContext).Assembly)
            .ConfigureQueries(c =>
            {
                c.AddHandlersFromAssembly(typeof(SegurancaConfigureWorkContext).Assembly);
            });
    }
}
