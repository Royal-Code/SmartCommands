using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Estoques;

public interface IAdicionarEntradaEstoqueHandler
{
    public Task<Result> HandleAsync(Guid produtoId, AdicionarEntradaEstoque command, CancellationToken ct);
}
