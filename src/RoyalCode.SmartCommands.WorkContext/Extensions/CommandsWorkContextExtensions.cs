using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalCode.SmartCommands.WorkContext.Adapters;
using RoyalCode.SmartCommands.WorkContext.Internals;
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
    /// <param name="configureOptions">An optional action to configure the <see cref="WorkContextAdapterOptions"/>.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static IUnitOfWorkBuilder<TDbContext> AddUnitOfWorkAccessor<TDbContext>(
        this IUnitOfWorkBuilder<TDbContext> builder,
        Action<WorkContextAdapterOptions>? configureOptions = null)
        where TDbContext : DbContext
    {
        builder.Services.AddConcurrencyRetryProblems();

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

        builder.Services.AddTransient(typeof(IRepositoryAccessor<>), typeof(RepositoryAdapter<>));

        if (configureOptions is not null)
        {
            builder.Services.Configure(configureOptions);
        }

        return builder;
    }

    /// <summary>
    /// <para>
    ///     Adds the <see cref="UnitOfWorkAccessor{TWorkContext}"/> as a service to the <see cref="IServiceCollection"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="TDbContext">The type of the <see cref="DbContext"/>.</typeparam>
    /// <param name="builder">The <see cref="IUnitOfWorkBuilder{TDbContext}"/> instance.</param>
    /// <param name="configureOptions">An optional action to configure the <see cref="WorkContextAdapterOptions"/>.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static IWorkContextBuilder<TDbContext> AddUnitOfWorkAccessor<TDbContext>(
        this IWorkContextBuilder<TDbContext> builder,
        Action<WorkContextAdapterOptions>? configureOptions = null)
        where TDbContext : DbContext
    {
        builder.Services.AddConcurrencyRetryProblems();

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

        builder.Services.AddTransient(typeof(IRepositoryAccessor<>), typeof(RepositoryAdapter<>));

        if (configureOptions is not null)
        {
            builder.Services.Configure(configureOptions);
        }

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
    public static IServiceCollection AddUnitOfWorkAccessor<TWorkContext>(this IServiceCollection services)
        where TWorkContext : IWorkContext
    {
        services.AddConcurrencyRetryProblems();

        services
            .AddScoped<UnitOfWorkAccessor<TWorkContext>>()
            .AddScoped<IUnitOfWorkAccessor<TWorkContext>>(
                sp => sp.GetRequiredService<UnitOfWorkAccessor<TWorkContext>>())
            .AddScoped<IRepositoriesAccessor<TWorkContext>>(
                sp => sp.GetRequiredService<UnitOfWorkAccessor<TWorkContext>>());

        services.AddTransient(typeof(IRepositoryAccessor<>), typeof(RepositoryAdapter<>));

        return services;
    }

    /// <summary>
    /// Adds the optimistic-concurrency retry problem factory and options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddConcurrencyRetryProblems(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<RetryOnConcurrencyOptions>();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                Microsoft.Extensions.Options.IConfigureOptions<RetryOnConcurrencyOptions>,
                ConfigureRetryOnConcurrencyOptions>());

        services.TryAddScoped<IConcurrencyRetryProblemFactory, DefaultConcurrencyRetryProblemFactory>();

        return services;
    }

    /// <summary>
    /// Registers a retry-exhausted problem factory for a command operation.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="operation">The semantic operation key.</param>
    /// <param name="factory">The problem factory.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddConcurrencyRetryProblem<TCommand>(
        this IServiceCollection services,
        string operation,
        ConcurrencyRetryProblemDelegate<TCommand> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return services.AddConcurrencyRetryProblem<TCommand>(
            operation,
            (_, command, context) => factory(command, context));
    }

    /// <summary>
    /// Registers a retry-exhausted problem factory for a command operation, with access to DI.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="operation">The semantic operation key.</param>
    /// <param name="factory">The problem factory.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddConcurrencyRetryProblem<TCommand>(
        this IServiceCollection services,
        string operation,
        ConcurrencyRetryProblemServiceDelegate<TCommand> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(factory);

        services.AddConcurrencyRetryProblems();

        services.AddSingleton<IConcurrencyRetryProblemRegistration>(
            new ConcurrencyRetryProblemRegistration<TCommand>(operation, factory.Invoke));

        return services;
    }

    /// <summary>
    /// Registers a generic retry-exhausted problem factory for an operation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="operation">The semantic operation key.</param>
    /// <param name="factory">The problem factory.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddConcurrencyRetryProblem(
        this IServiceCollection services,
        string operation,
        ConcurrencyRetryProblemDelegate<object> factory)
    {
        return services.AddConcurrencyRetryProblem<object>(operation, factory);
    }

    /// <summary>
    /// Registers a generic retry-exhausted problem factory for an operation, with access to DI.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="operation">The semantic operation key.</param>
    /// <param name="factory">The problem factory.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddConcurrencyRetryProblem(
        this IServiceCollection services,
        string operation,
        ConcurrencyRetryProblemServiceDelegate<object> factory)
    {
        return services.AddConcurrencyRetryProblem<object>(operation, factory);
    }

    /// <summary>
    /// Registers a retry-exhausted problem provider for a command operation.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="operation">The semantic operation key.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddConcurrencyRetryProblemProvider<TCommand, TProvider>(
        this IServiceCollection services,
        string operation)
        where TProvider : class, IConcurrencyRetryProblemProvider<TCommand>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        services.AddConcurrencyRetryProblems();
        services.TryAddTransient<TProvider>();

        services.AddSingleton<IConcurrencyRetryProblemRegistration>(
            new ConcurrencyRetryProblemRegistration<TCommand>(
                operation,
                (sp, command, context) => sp.GetRequiredService<TProvider>().Create(command, context)));

        return services;
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
