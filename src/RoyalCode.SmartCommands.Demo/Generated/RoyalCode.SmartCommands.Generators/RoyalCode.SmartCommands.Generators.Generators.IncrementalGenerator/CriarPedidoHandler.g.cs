using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.Demo.Commands.Pedidos;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo.Commands.Pedidos.Internals;

public class CriarPedidoHandler : ICriarPedidoHandler
{
    private readonly IUnitOfWorkAccessor<IWorkContext> accessor;
    private readonly DemoDbContext db;

    public CriarPedidoHandler(IUnitOfWorkAccessor<IWorkContext> accessor, DemoDbContext db)
    {
        this.accessor = accessor;
        this.db = db;
    }

    public async Task<Result<Pedido>> HandleAsync(CriarPedido command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        return await command.Execute(db, ct)
            .ContinueAsync(this.accessor, async (e, a) => await a.AddEntityAsync(e, ct))
            .ContinueAsync(this.accessor, async (_, a) => await a.CompleteAsync(ct));
    }
}
