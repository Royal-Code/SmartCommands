using Microsoft.AspNetCore.Mvc;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Commands.Movies;

/// <summary>
/// Comando de demonstração do binding de parâmetros externos (Fase 5/DF2/DF3) e das validações adicionais
/// (Fase 6/DF13): valores de rota, query, header, serviço de DI e tipo com <c>BindAsync</c> customizado
/// chegam ao método do comando via <c>[WithParameter]</c>, e a plataforma é validada assincronamente por um
/// serviço antes da execução.
/// </summary>
[MapGroup("playground")]
[MapPost("/{movieId}/views", "registrar-visualizacao")]
public class RegistrarVisualizacao
{
    /// <summary>Plataforma informada no corpo da requisição (body implícito do POST).</summary>
    public string? Plataforma { get; set; }

    /// <summary>
    /// Validação adicional assíncrona (DF13) dependente de serviço: roda antes da execução do comando e
    /// interrompe o handler com um problema quando a plataforma não é aceita.
    /// </summary>
    [CommandValidation]
    internal async Task<Result> ValidarPlataformaAsync(IPlataformasPermitidas plataformas, CancellationToken ct)
    {
        return await plataformas.PermitidaAsync(Plataforma, ct)
            ? Result.Ok()
            : Problems.InvalidParameter($"A plataforma '{Plataforma}' não é aceita.", nameof(Plataforma));
    }

    [Command]
    public VisualizacaoRegistrada Executar(
        [WithParameter, FromRoute(Name = "movieId")] int movieId,
        [WithParameter, FromQuery(Name = "origem")] string? origem,
        [WithParameter, FromHeader(Name = "x-demo-user")] string? usuario,
        [WithParameter] MomentoDaRequisicao momento,
        [WithParameter] IRelogioDemo relogio)
    {
        return new VisualizacaoRegistrada(
            movieId,
            Plataforma,
            origem,
            usuario,
            momento.Valor,
            relogio.Agora());
    }
}

/// <summary>Resumo da visualização registrada; ecoa os valores vinculados para fins de demonstração.</summary>
public sealed record VisualizacaoRegistrada(
    int MovieId,
    string? Plataforma,
    string? Origem,
    string? Usuario,
    DateTimeOffset Momento,
    DateTimeOffset RegistradoEm);
