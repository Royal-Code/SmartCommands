using RoyalCode.SmartSearch;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Contracts.Usuarios;

public class UsuarioFiltro
{
    [Criterion("Email.Value")]
    public string? Email { get; set; }
}
