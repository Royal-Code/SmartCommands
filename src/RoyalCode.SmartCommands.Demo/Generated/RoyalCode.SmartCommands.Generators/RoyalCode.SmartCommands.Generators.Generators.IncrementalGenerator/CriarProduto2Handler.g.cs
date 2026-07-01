using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos.Internals;

public class CriarProduto2Handler : ICriarProduto2Handler
{
    private readonly IUnitOfWorkAccessor<IWorkContext> accessor;
    private readonly DemoDbContext db;

    public CriarProduto2Handler(IUnitOfWorkAccessor<IWorkContext> accessor, DemoDbContext db)
    {
        this.accessor = accessor;
        this.db = db;
    }

    public async Task<Result<Produto>> HandleAsync(CriarProduto2 command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.Execute(db, ct)
            .ContinueAsync(this.accessor, async (e, a) => await a.AddEntityAsync(e, ct))
            .ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}
