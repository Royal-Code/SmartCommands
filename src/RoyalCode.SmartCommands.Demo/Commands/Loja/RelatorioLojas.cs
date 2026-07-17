using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Lojas;

/// <summary>
/// Endpoint com policy da matriz Minimal API (Fase 9): o <c>WithPolicy</c> gera
/// <c>RequireAuthorization("relatorios")</c> no endpoint. A demo nao configura autenticacao,
/// entao este endpoint serve para inspecionar a metadata de autorizacao gerada.
/// </summary>
[MapGroup("lojas")]
[MapGet("/relatorio", "relatorio-lojas")]
[WithPolicy("relatorios")]
[WithSummary("Relatorio de lojas")]
public partial class RelatorioLojas
{
    [Command]
    internal async Task<Result<RelatorioLojasResultado>> Execute(DemoDbContext db, CancellationToken ct)
    {
        var total = await db.Lojas.CountAsync(ct);
        var ativas = await db.Lojas.CountAsync(l => l.Ativa, ct);
        return new RelatorioLojasResultado(total, ativas);
    }
}

/// <summary>Resumo quantitativo das lojas.</summary>
public sealed record RelatorioLojasResultado(int Total, int Ativas);
