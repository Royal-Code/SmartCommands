namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain.Servicos;

public interface IAcessoService
{
    bool UsuarioTemPermissao(Usuario usuario, string codigoPermissao);
}
