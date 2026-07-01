using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Estoques;

public interface ILiberarReservaEstoqueHandler
{
    public Task<Result> HandleAsync(Guid produtoId, LiberarReservaEstoque command, CancellationToken ct);
}
