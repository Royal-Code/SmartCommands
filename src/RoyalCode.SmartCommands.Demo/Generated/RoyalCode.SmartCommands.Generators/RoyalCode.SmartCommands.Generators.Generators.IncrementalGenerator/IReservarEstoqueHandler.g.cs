using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Estoques;

public interface IReservarEstoqueHandler
{
    public Task<Result> HandleAsync(Guid produtoId, ReservarEstoque command, CancellationToken ct);
}
