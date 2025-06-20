using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Lojas;

public interface ICriarLojaHandler
{
    public Task<Result<Loja>> HandleAsync(CriarLoja command, CancellationToken ct);
}
