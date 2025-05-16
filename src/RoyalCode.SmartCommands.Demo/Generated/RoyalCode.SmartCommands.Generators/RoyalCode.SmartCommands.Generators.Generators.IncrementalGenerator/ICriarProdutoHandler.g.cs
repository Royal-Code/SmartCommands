using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

public interface ICriarProdutoHandler
{
    public Task<Result<Produto>> HandleAsync(CriarProduto command, CancellationToken ct);
}
