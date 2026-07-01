using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Lojas;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo.Commands.Lojas.Internals;

public class CriarLojaHandler : ICriarLojaHandler
{
    private readonly IUnitOfWorkAccessor<IWorkContext> accessor;

    public CriarLojaHandler(IUnitOfWorkAccessor<IWorkContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Loja>> HandleAsync(CriarLoja command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        var commandResult = command.Execute();

        await this.accessor.AddEntityAsync(commandResult, ct);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}
