using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

/// <summary>
/// GET da matriz Minimal API (Fase 9): comando sem corpo (a classe nao possui propriedades publicas),
/// com o valor externo <c>sku</c> vindo da query por inferencia (DF2) e dependencia resolvida por DI.
/// </summary>
[MapGroup("produtos")]
[MapGet("/sku-disponivel", "verificar-sku-disponivel")]
[WithSummary("Verificar disponibilidade de SKU")]
[WithDescription("Indica se o SKU informado ainda nao esta em uso por outro produto.")]
public partial class VerificarSkuDisponivel
{
    [Command]
    internal async Task<Result<SkuDisponibilidade>> Execute(
        DemoDbContext db,
        [WithParameter] string sku,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return Problems.InvalidParameter("O SKU deve ser informado.", property: nameof(sku));

        var emUso = await db.Produtos.AnyAsync(p => p.Sku == sku, ct);
        return new SkuDisponibilidade(sku, !emUso);
    }
}

/// <summary>Resultado da verificacao de disponibilidade de SKU.</summary>
public sealed record SkuDisponibilidade(string Sku, bool Disponivel);
