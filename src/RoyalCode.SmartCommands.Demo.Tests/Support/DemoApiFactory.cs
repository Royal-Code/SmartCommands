using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartCommands.WorkContext;
using RoyalCode.SmartCommands.WorkContext.Extensions;
using RoyalCode.SmartCommands.WorkContext.Internals;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.UnitOfWork;

namespace RoyalCode.SmartCommands.Demo.Tests.Support;

internal sealed class DemoApiFactory : WebApplicationFactory<Program>
{
	private readonly SqliteConnection connection = CreateOpenConnection();
	private readonly Action<IServiceCollection>? configureTestServices;
	private readonly string environment;

	public DemoApiFactory(
		Action<IServiceCollection>? configureTestServices = null,
		string environment = "Testing")
	{
		this.configureTestServices = configureTestServices;
		this.environment = environment;
	}

	public ConcurrencyFailureController ConcurrencyFailures { get; } = new();

	public async Task ResetDatabaseAsync()
	{
		using var scope = Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<DemoDbContext>();

		await db.Database.EnsureDeletedAsync();
		await db.Database.EnsureCreatedAsync();

		ConcurrencyFailures.Reset();
	}

	public static void UseTypedRetryProblemDelegate(IServiceCollection services)
	{
		RemoveRetryProblemRegistrations(services);
		ConfigureTwoAttempts(services);

		services.AddConcurrencyRetryProblem<EditarProduto>(
			"demo.produtos.editar",
			static (command, context) => Problems.InvalidState(
				$"typed:{command.Nome}:{context.Operation}",
				typeId: "demo.typed_concurrency"));
	}

	public static void UseServiceRetryProblemDelegate(IServiceCollection services)
	{
		RemoveRetryProblemRegistrations(services);
		ConfigureTwoAttempts(services);

		services.AddSingleton(new RetryProblemMessageSource("service"));
		services.AddConcurrencyRetryProblem<EditarProduto>(
			"demo.produtos.editar",
			static (sp, command, context) =>
			{
				var source = sp.GetRequiredService<RetryProblemMessageSource>();
				return Problems.InvalidState(
					$"{source.Value}:{command.Nome}:{context.Operation}",
					typeId: "demo.service_concurrency");
			});
	}

	public static void UseProviderRetryProblem(IServiceCollection services)
	{
		RemoveRetryProblemRegistrations(services);
		ConfigureTwoAttempts(services);

		services.AddConcurrencyRetryProblemProvider<EditarProduto, EditarProdutoRetryProblemProvider>(
			"demo.produtos.editar");
	}

	public static void UseFallbackRetryProblem(IServiceCollection services)
	{
		RemoveRetryProblemRegistrations(services);
		ConfigureTwoAttempts(services);

		services.Configure<RetryOnConcurrencyOptions>(options =>
		{
			options.ExhaustedProblemDetail = "fallback concurrency conflict";
			options.ExhaustedProblemTypeId = "demo.fallback_concurrency";
		});
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment(environment);

		builder.ConfigureTestServices(services =>
		{
			services.RemoveAll<DbContextOptions<DemoDbContext>>();
			services.RemoveAll<DemoDbContext>();

			services.AddSingleton(connection);
			services.AddSingleton(ConcurrencyFailures);
			services.AddSingleton<ConcurrencySaveChangesInterceptor>();
			services.AddDbContext<DemoDbContext>((sp, options) =>
			{
				options
					.UseSqlite(sp.GetRequiredService<SqliteConnection>())
					.AddInterceptors(sp.GetRequiredService<ConcurrencySaveChangesInterceptor>());
			});

			configureTestServices?.Invoke(services);
		});
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
			connection.Dispose();
	}

	private static SqliteConnection CreateOpenConnection()
	{
		var connection = new SqliteConnection("Data Source=:memory:");
		connection.Open();
		return connection;
	}

	private static void ConfigureTwoAttempts(IServiceCollection services)
	{
		services.Configure<RetryOnConcurrencyOptions>(options => options.MaxAttempts = 2);
	}

	private static void RemoveRetryProblemRegistrations(IServiceCollection services)
	{
		var descriptors = services
			.Where(d => d.ServiceType == typeof(IConcurrencyRetryProblemRegistration))
			.ToArray();

		foreach (var descriptor in descriptors)
			services.Remove(descriptor);
	}

	private sealed record RetryProblemMessageSource(string Value);

	private sealed class EditarProdutoRetryProblemProvider : IConcurrencyRetryProblemProvider<EditarProduto>
	{
		public Problem Create(EditarProduto command, ConcurrencyRetryProblemContext context)
		{
			return Problems.InvalidState(
				$"provider:{command.Nome}:{context.Operation}",
				typeId: "demo.provider_concurrency");
		}
	}

	private sealed class ConcurrencySaveChangesInterceptor(
		ConcurrencyFailureController concurrencyFailures)
		: SaveChangesInterceptor
	{
		public override InterceptionResult<int> SavingChanges(
			DbContextEventData eventData,
			InterceptionResult<int> result)
		{
			if (eventData.Context is not null)
				concurrencyFailures.ThrowIfConfigured(eventData.Context.ChangeTracker);

			return result;
		}

		public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
			DbContextEventData eventData,
			InterceptionResult<int> result,
			CancellationToken cancellationToken = default)
		{
			if (eventData.Context is not null)
				concurrencyFailures.ThrowIfConfigured(eventData.Context.ChangeTracker);

			return ValueTask.FromResult(result);
		}
	}
}

internal sealed class ConcurrencyFailureController
{
	public const string RetryOnceProductName = "Produto retry transitorio";
	public const string RetryAlwaysProductName = "Produto retry esgotado";
	public const string RetryOnceWithDbUpdateProductName = "Produto retry dbupdate";

	public int Failures { get; private set; }

	private bool failNextStockSave;

	private bool failStockSavesAlways;

	public void Reset()
	{
		Failures = 0;
		failNextStockSave = false;
		failStockSavesAlways = false;
	}

	public void FailNextStockSave()
	{
		failNextStockSave = true;
		failStockSavesAlways = false;
		Failures = 0;
	}

	public void FailStockSavesAlways()
	{
		failStockSavesAlways = true;
		failNextStockSave = false;
		Failures = 0;
	}

	public void ThrowIfConfigured(ChangeTracker changeTracker)
	{
		var stockEntry = changeTracker
			.Entries<ProdutoEstoque>()
			.FirstOrDefault(e => e.State == EntityState.Modified);

		if (stockEntry is not null && failNextStockSave && Failures == 0)
		{
			Failures++;
			failNextStockSave = false;
			throw new ConcurrencyException("Test transient stock concurrency conflict.", new InvalidOperationException());
		}

		if (stockEntry is not null && failStockSavesAlways)
		{
			Failures++;
			throw new ConcurrencyException("Test persistent stock concurrency conflict.", new InvalidOperationException());
		}

		var entry = changeTracker
			.Entries<Produto>()
			.FirstOrDefault(e => e.State == EntityState.Modified
				&& (e.Entity.Nome == RetryOnceProductName
					|| e.Entity.Nome == RetryAlwaysProductName
					|| e.Entity.Nome == RetryOnceWithDbUpdateProductName));

		if (entry is null)
			return;

		if (entry.Entity.Nome == RetryOnceProductName && Failures == 0)
		{
			Failures++;
			throw new ConcurrencyException("Test transient concurrency conflict.", new InvalidOperationException());
		}

		// Lança a exceção crua do EF para exercitar a conversão DbUpdateConcurrencyException -> ConcurrencyException
		// feita em UnitOfWork.SaveAsync (que é o que o retry captura no fluxo real).
		if (entry.Entity.Nome == RetryOnceWithDbUpdateProductName && Failures == 0)
		{
			Failures++;
			throw new DbUpdateConcurrencyException("Test transient db-update concurrency conflict.");
		}

		if (entry.Entity.Nome == RetryAlwaysProductName)
		{
			Failures++;
			throw new ConcurrencyException("Test persistent concurrency conflict.", new InvalidOperationException());
		}
	}
}
