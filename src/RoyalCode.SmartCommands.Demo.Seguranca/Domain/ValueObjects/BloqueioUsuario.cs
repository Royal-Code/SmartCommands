using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa o estado de bloqueio de um usuário.
/// </summary>
public record class BloqueioUsuario
{
    // Constructors
    public BloqueioUsuario(
        bool bloqueado = false,
        DateTimeOffset? dataBloqueio = null,
        TipoBloqueio? tipo = null,
        string? motivo = null)
    {
        Bloqueado = bloqueado;
        DataBloqueio = dataBloqueio;
        Tipo = tipo;
        Motivo = motivo;
    }

    // Properties
    public bool Bloqueado { get; private set; }
    public DateTimeOffset? DataBloqueio { get; private set; }
    public TipoBloqueio? Tipo { get; private set; }
    public string? Motivo { get; private set; }

    // Methods
    public Result Bloquear(TipoBloqueio tipo, string motivo)
    {
        if (Bloqueado)
        {
            return Problems.InvalidState("Usuário já está bloqueado.");
        }

        Bloqueado = true;
        DataBloqueio = DateTimeOffset.UtcNow;
        Tipo = tipo;
        Motivo = motivo;

        return Result.Ok();
    }

    public Result RemoverBloquio()
    {
        if (!Bloqueado)
        {
            return Problems.InvalidState("Usuário não está bloqueado.");
        }

        Bloqueado = false;
        DataBloqueio = null;
        Tipo = null;
        Motivo = null;

        return Result.Ok();
    }
}
