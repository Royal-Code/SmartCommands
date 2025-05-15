using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands;

public interface ICriarProdutoHandler
{
    public Task<Result<Produto>> HandleAsync(CriarProduto command, CancellationToken ct);
}
