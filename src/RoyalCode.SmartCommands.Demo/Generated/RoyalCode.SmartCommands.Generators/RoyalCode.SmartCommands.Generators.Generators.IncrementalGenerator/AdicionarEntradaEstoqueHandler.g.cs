using Microsoft.Extensions.Options;
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Estoques;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartCommands.WorkContext;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo.Commands.Estoques.Internals;

public class AdicionarEntradaEstoqueHandler<TContext> : IAdicionarEntradaEstoqueHandler
    where TContext : IWorkContext
{
    private readonly IUnitOfWorkAccessor<TContext> accessor;
    private readonly IOptions<RetryOnConcurrencyOptions> retryOptions;
    private readonly IConcurrencyRetryProblemFactory retryProblemFactory;
    private readonly DemoDbContext db;

    public AdicionarEntradaEstoqueHandler(IUnitOfWorkAccessor<TContext> accessor, IOptions<RetryOnConcurrencyOptions> retryOptions, IConcurrencyRetryProblemFactory retryProblemFactory, DemoDbContext db)
    {
        this.accessor = accessor;
        this.retryOptions = retryOptions;
        this.retryProblemFactory = retryProblemFactory;
        this.db = db;
    }

    public async Task<Result> HandleAsync(Guid produtoId, AdicionarEntradaEstoque command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        return await this.accessor.Context.RetryOnConcurrencyAsync(
            async () =>
            {
                await this.accessor.BeginAsync(ct);

                Problem? notFoundProblem;

                var produtoEntry = await this.accessor.FindEntityAsync<Produto, Guid>(produtoId, ct);
                if (produtoEntry.NotFound(out notFoundProblem))
                    return notFoundProblem;
                var produto = produtoEntry.Entity;

                return await command.Execute(produto, db, ct).ContinueAsync(this.accessor, static async (a, ct) => await a.CompleteAsync(ct), ct);
            },
            this.retryOptions.Value,
            onExhausted: () => this.retryProblemFactory.Create(command, "demo.estoques.adicionar"),
            ct: ct);
    }
}
