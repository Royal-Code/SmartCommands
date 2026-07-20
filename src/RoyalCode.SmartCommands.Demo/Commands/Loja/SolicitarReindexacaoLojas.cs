using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Lojas;

/// <summary>
/// Vitrine da Fase 3 (DF17): <c>MapAcceptedRoute</c> com rota estática sobre um comando que retorna
/// <c>Result</c> (sem valor) responde <c>202 Accepted</c> com header <c>Location</c> fixo e sem corpo —
/// a Location do <c>202</c> é opcional e, sem valor de sucesso, a rota é necessariamente estática.
/// A Demo não executa reindexação real; o caso existe para tornar o contrato HTTP observável.
/// </summary>
[MapGroup("lojas")]
[MapPost("/reindexacoes", "solicitar-reindexacao-lojas")]
[MapAcceptedRoute("reindexacoes/status")]
[WithTags("Lojas", "Administracao")]
[WithSummary("Solicitar Reindexação de Lojas")]
public sealed class SolicitarReindexacaoLojas
{
    [Command]
    internal Result Execute() => Result.Ok();
}
