using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.EntityFramework.Adapters;

namespace RoyalCode.SmartCommands.EntityFramework.Extensions;

/// <summary>
/// Extensions for add <see cref="IUnitOfWorkAccessor{T}"/> using Entity Framework Core.
/// </summary>
public static class CommandsDbContextExtensions
{
    /// <summary>
    /// Adds the <see cref="IUnitOfWorkAccessor{T}"/> implementation using Entity Framework Core.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddUnitOfWorkAccessor<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        return services
            .AddScoped<DbContextAccessor<TContext>>()
            .AddScoped<IUnitOfWorkAccessor<TContext>>(sp => sp.GetRequiredService<DbContextAccessor<TContext>>())
            .AddScoped<IRepositoriesAccessor<TContext>>(sp => sp.GetRequiredService<DbContextAccessor<TContext>>());
    }

    /// <summary>
    /// Adds the <see cref="IUnitOfWorkAccessor{TContextBase}"/> implementation using Entity Framework Core.
    /// </summary>
    /// <typeparam name="TContextBase">The type of the context base.</typeparam>
    /// <typeparam name="TContextImpl">The type of the context implementation.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddUnitOfWorkAccessor<TContextBase, TContextImpl>(this IServiceCollection services)
        where TContextBase: DbContext
        where TContextImpl : TContextBase
    {
        return services
            .AddScoped<DbContextAccessor<TContextImpl>>()
            .AddScoped<IUnitOfWorkAccessor<TContextBase>>(sp =>
                sp.GetRequiredService<DbContextAccessor<TContextImpl>>())
            .AddScoped<IRepositoriesAccessor<TContextBase>>(sp =>
                sp.GetRequiredService<DbContextAccessor<TContextImpl>>());
    }
}
