using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.WorkContext.Adapters;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.UnitOfWork.EntityFramework.Configurations;
using RoyalCode.WorkContext;
using RoyalCode.WorkContext.EntityFramework;
using RoyalCode.WorkContext.EntityFramework.Configurations;

namespace RoyalCode.SmartCommands.WorkContext.Extensions;

/// <summary>
/// Extension methods for <see cref="IWorkContext"/> to configure adapters for commands.
/// </summary>
public static class CommandsWorkContextExtensions
{
    /// <summary>
    /// <para>
    ///     Adds the <see cref="UnitOfWorkAccessor{TWorkContext}"/> as a service to the <see cref="IServiceCollection"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="TDbContext">The type of the <see cref="DbContext"/>.</typeparam>
    /// <param name="builder">The <see cref="IUnitOfWorkBuilder{TDbContext}"/> instance.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static IUnitOfWorkBuilder<TDbContext> AddUnitOfWorkAdapter<TDbContext>(this IUnitOfWorkBuilder<TDbContext> builder)
        where TDbContext : DbContext
    {
         builder.Services
            .AddScoped<UnitOfWorkAccessor<IWorkContext<TDbContext>>>()
           .AddScoped<IUnitOfWorkAccessor<IWorkContext>>(
               sp => sp.GetRequiredService<UnitOfWorkAccessor<IWorkContext<TDbContext>>>())
           .AddScoped<IUnitOfWorkAccessor<IWorkContext<TDbContext>>>(
               sp => sp.GetRequiredService<UnitOfWorkAccessor<IWorkContext<TDbContext>>>())
           .AddScoped<IRepositoriesAccessor<IWorkContext>>(
               sp => sp.GetRequiredService<UnitOfWorkAccessor<IWorkContext<TDbContext>>>())
           .AddScoped<IRepositoriesAccessor<IWorkContext<TDbContext>>>(
               sp => sp.GetRequiredService<UnitOfWorkAccessor<IWorkContext<TDbContext>>>());

        return builder;
    }

    /// <summary>
    /// <para>
    ///     Adds the <see cref="UnitOfWorkAccessor{TWorkContext}"/> as a service to the <see cref="IServiceCollection"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="TDbContext">The type of the <see cref="DbContext"/>.</typeparam>
    /// <param name="builder">The <see cref="IUnitOfWorkBuilder{TDbContext}"/> instance.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static IWorkContextBuilder<TDbContext> AddUnitOfWorkAdapter<TDbContext>(this IWorkContextBuilder<TDbContext> builder)
        where TDbContext : DbContext
    {
        builder.Services
           .AddScoped<UnitOfWorkAccessor<IWorkContext<TDbContext>>>()
           .AddScoped<IUnitOfWorkAccessor<IWorkContext>>(
               sp => sp.GetRequiredService<UnitOfWorkAccessor<IWorkContext<TDbContext>>>())
           .AddScoped<IUnitOfWorkAccessor<IWorkContext<TDbContext>>>(
               sp => sp.GetRequiredService<UnitOfWorkAccessor<IWorkContext<TDbContext>>>())
           .AddScoped<IRepositoriesAccessor<IWorkContext>>(
               sp => sp.GetRequiredService<UnitOfWorkAccessor<IWorkContext<TDbContext>>>())
           .AddScoped<IRepositoriesAccessor<IWorkContext<TDbContext>>>(
               sp => sp.GetRequiredService<UnitOfWorkAccessor<IWorkContext<TDbContext>>>());

        return builder;
    }

    /// <summary>
    /// <para>
    ///     Adds the <see cref="UnitOfWorkAccessor{TWorkContext}"/> as a service to the <see cref="IServiceCollection"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="TWorkContext">The type of the <see cref="IWorkContext"/>.</typeparam>  
    /// <param name="services">The <see cref="IServiceCollection"/> instance.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddUnitOfWorkAdapter<TWorkContext>(this IServiceCollection services)
        where TWorkContext : IWorkContext
    {
        return services
            .AddScoped<UnitOfWorkAccessor<TWorkContext>>()
            .AddScoped<IUnitOfWorkAccessor<TWorkContext>>(
                sp => sp.GetRequiredService<UnitOfWorkAccessor<TWorkContext>>())
            .AddScoped<IRepositoriesAccessor<TWorkContext>>(
                sp => sp.GetRequiredService<UnitOfWorkAccessor<TWorkContext>>());
    }

    /// <summary>
    /// <para>
    ///     Configures the <see cref="WorkContextAdapterOptions"/> for the <see cref="IWorkContext"/> adapter.
    /// </para>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance.</param>
    /// <param name="configureOptions">The action to configure the options.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection ConfigureWorkContextAdapterOptions(
        this IServiceCollection services,
        Action<WorkContextAdapterOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);

        return services;
    }
}
