using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

public interface IEditarProdutoHandler
{
    public Task<Result> HandleAsync(Guid produtoId, EditarProduto command, CancellationToken ct);
}
