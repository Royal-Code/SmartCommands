using Microsoft.AspNetCore.Mvc;

namespace RoyalCode.SmartCommands.Demo.Commands.Movies;

/// <summary>
/// Comando de demonstração do binding de parâmetros externos (Fase 5/DF2/DF3): valores de rota, query,
/// header, serviço de DI e tipo com <c>BindAsync</c> customizado chegam ao método do comando via
/// <c>[WithParameter]</c>, com os atributos explícitos copiados apenas para o delegate Minimal API.
/// </summary>
[MapGroup("playground")]
[MapPost("/{movieId}/views", "registrar-visualizacao")]
public class RegistrarVisualizacao
{
    /// <summary>Plataforma informada no corpo da requisição (body implícito do POST).</summary>
    public string? Plataforma { get; set; }

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
