using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos.Internals;

public class CriarProduto2Handler : ICriarProduto2Handler
{
    private readonly IUnitOfWorkAccessor<IWorkContext> accessor;

    public CriarProduto2Handler(IUnitOfWorkAccessor<IWorkContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Produto>> HandleAsync(CriarProduto2 command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var commandResult = command.Execute();

        await this.accessor.AddEntityAsync(commandResult, ct);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}
