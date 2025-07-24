using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

public interface ICriarProduto2Handler
{
    public Task<Result<Produto>> HandleAsync(CriarProduto2 command, CancellationToken ct);
}
