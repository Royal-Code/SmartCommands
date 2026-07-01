using Microsoft.Extensions.Options;
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos.Internals;

public class ReativarProdutoHandler<TContext> : IReativarProdutoHandler
    where TContext : IWorkContext
{
    private readonly IUnitOfWorkAccessor<TContext> accessor;
    private readonly IOptions<RetryOnConcurrencyOptions> retryOptions;

    public ReativarProdutoHandler(IUnitOfWorkAccessor<TContext> accessor, IOptions<RetryOnConcurrencyOptions> retryOptions)
    {
        this.accessor = accessor;
        this.retryOptions = retryOptions;
    }

    public async Task<Result> HandleAsync(Guid produtoId, ReativarProduto command, CancellationToken ct)
    {
        return await this.accessor.Context.RetryOnConcurrencyAsync(
            async () =>
            {
                await this.accessor.BeginAsync(ct);

                Problem? notFoundProblem;

                var produtoEntry = await this.accessor.FindEntityAsync<Produto, Guid>(produtoId, ct);
                if (produtoEntry.NotFound(out notFoundProblem))
                    return notFoundProblem;
                var produto = produtoEntry.Entity;

                return await command.Execute(produto).ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
            },
            this.retryOptions.Value,
            ct: ct);
    }
}
