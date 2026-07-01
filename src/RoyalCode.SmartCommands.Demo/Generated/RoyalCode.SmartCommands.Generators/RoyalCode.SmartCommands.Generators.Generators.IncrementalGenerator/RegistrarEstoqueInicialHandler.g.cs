using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Estoques;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo.Commands.Estoques.Internals;

public class RegistrarEstoqueInicialHandler<TContext> : IRegistrarEstoqueInicialHandler
    where TContext : IWorkContext
{
    private readonly IUnitOfWorkAccessor<TContext> accessor;
    private readonly DemoDbContext db;

    public RegistrarEstoqueInicialHandler(IUnitOfWorkAccessor<TContext> accessor, DemoDbContext db)
    {
        this.accessor = accessor;
        this.db = db;
    }

    public async Task<Result> HandleAsync(Guid produtoId, RegistrarEstoqueInicial command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        Problem? notFoundProblem;

        var produtoEntry = await this.accessor.FindEntityAsync<Produto, Guid>(produtoId, ct);
        if (produtoEntry.NotFound(out notFoundProblem))
            return notFoundProblem;
        var produto = produtoEntry.Entity;

        return await command.Execute(produto, db, ct).ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
    }
}
