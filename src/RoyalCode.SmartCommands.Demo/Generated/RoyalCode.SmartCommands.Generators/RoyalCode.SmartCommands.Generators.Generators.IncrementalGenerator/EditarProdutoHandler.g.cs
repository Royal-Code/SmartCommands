using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext.Abstractions;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos.Internals;

public class EditarProdutoHandler : IEditarProdutoHandler
{
    private readonly IUnitOfWorkAccessor<IWorkContext> accessor;

    public EditarProdutoHandler(IUnitOfWorkAccessor<IWorkContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result> HandleAsync(Guid produtoId, EditarProduto command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        Problem? notFoundProblem;

        var produtoEntry = await this.accessor.FindEntityAsync<Produto, Guid>(produtoId, ct);
        if (produtoEntry.NotFound(out notFoundProblem))
            return notFoundProblem;
        var produto = produtoEntry.Entity;

        command.Execute(produto);

        return await this.accessor.CompleteAsync(ct);
    }
}
