using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

public interface IDesativarProdutoHandler
{
    public Task<Result> HandleAsync(Guid produtoId, DesativarProduto command, CancellationToken ct);
}
