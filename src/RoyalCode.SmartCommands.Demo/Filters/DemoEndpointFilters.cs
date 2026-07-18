using RoyalCode.SmartCommands.Demo.Commands.Movies;

namespace RoyalCode.SmartCommands.Demo.Filters;

/// <summary>
/// Filtros de endpoint da Fase 10 (DF23): aplicados por <c>[WithEndpointFilter&lt;T&gt;]</c> na ordem
/// declarada; cada filtro registra sua passagem no header <c>X-Demo-Filtros</c>, permitindo aos testes
/// observar ordem de execução e ativação via DI (o <see cref="FiltroCarimbo"/> depende de
/// <see cref="IRelogioDemo"/> resolvido pelo container).
/// </summary>
public sealed class FiltroAuditoria : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.Append("X-Demo-Filtros", "auditoria");
        return await next(context);
    }
}

/// <summary>Segundo filtro da cadeia; comprova a ativação com dependência de DI.</summary>
public sealed class FiltroCarimbo : IEndpointFilter
{
    private readonly IRelogioDemo relogio;

    public FiltroCarimbo(IRelogioDemo relogio)
    {
        this.relogio = relogio;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.Append("X-Demo-Filtros", "carimbo");
        context.HttpContext.Response.Headers.Append(
            "X-Demo-Carimbo",
            relogio.Agora().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        return await next(context);
    }
}
