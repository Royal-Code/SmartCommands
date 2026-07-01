using Microsoft.Extensions.Options;
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos.Internals;

public class DesativarProdutoHandler<TContext> : IDesativarProdutoHandler
    where TContext : IWorkContext
{
    private readonly IUnitOfWorkAccessor<TContext> accessor;
    private readonly IOptions<RetryOnConcurrencyOptions> retryOptions;

    public DesativarProdutoHandler(IUnitOfWorkAccessor<TContext> accessor, IOptions<RetryOnConcurrencyOptions> retryOptions)
    {
        this.accessor = accessor;
        this.retryOptions = retryOptions;
    }

    public async Task<Result> HandleAsync(Guid produtoId, DesativarProduto command, CancellationToken ct)
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

                command.Execute(produto);

                return await this.accessor.CompleteAsync(ct);
            },
            this.retryOptions.Value,
            ct: ct);
    }
}
