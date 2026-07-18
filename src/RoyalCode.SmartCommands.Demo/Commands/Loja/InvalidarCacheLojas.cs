using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Lojas;

/// <summary>
/// Demonstra a seleção explícita de 200 OK em um endpoint DELETE que, sem o atributo, seria inferido
/// como 204 No Content. A Demo não mantém um cache real; o caso existe para tornar o contrato HTTP observável.
/// </summary>
[MapGroup("lojas")]
[MapDelete("/cache", "invalidar-cache-lojas")]
[WithResultStatus(HttpResultStatus.Ok)]
[WithTags("Lojas", "Administracao")]
public sealed class InvalidarCacheLojas
{
    [Command]
    internal Result Execute() => Result.Ok();
}
