using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands;
using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext.Abstractions;

namespace RoyalCode.SmartCommands.Demo.Commands.Internals;

public class CriarProdutoHandler : ICriarProdutoHandler
{
    private readonly IUnitOfWorkAccessor<IWorkContext> accessor;

    public CriarProdutoHandler(IUnitOfWorkAccessor<IWorkContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Produto>> HandleAsync(CriarProduto command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var commandResult = command.ExecuteAsync();

        await this.accessor.AddEntityAsync(commandResult, ct);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}
