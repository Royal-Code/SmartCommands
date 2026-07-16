using System.Globalization;
using System.Reflection;

namespace RoyalCode.SmartCommands.Demo.Commands.Movies;

/// <summary>
/// Serviço simples usado pelo comando <see cref="RegistrarVisualizacao"/> para demonstrar a inferência de
/// binding <c>FromServices</c> do Minimal API (DF2): um parâmetro <c>[WithParameter]</c> sem atributo cujo
/// tipo está registrado no DI é resolvido como serviço.
/// </summary>
public interface IRelogioDemo
{
    DateTimeOffset Agora();
}

/// <inheritdoc />
public sealed class RelogioDemo : IRelogioDemo
{
    public DateTimeOffset Agora() => DateTimeOffset.UtcNow;
}

/// <summary>
/// Tipo com <c>BindAsync</c> customizado (special type do Minimal API): demonstra que um parâmetro
/// <c>[WithParameter]</c> sem atributo usa o binding customizado do próprio tipo (DF2).
/// Lê a query <c>momento</c> quando presente; caso contrário usa o horário atual.
/// </summary>
public sealed class MomentoDaRequisicao
{
    public DateTimeOffset Valor { get; init; }

    public static ValueTask<MomentoDaRequisicao?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var raw = context.Request.Query["momento"].ToString();
        var valor = DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;

        return ValueTask.FromResult<MomentoDaRequisicao?>(new MomentoDaRequisicao { Valor = valor });
    }
}
