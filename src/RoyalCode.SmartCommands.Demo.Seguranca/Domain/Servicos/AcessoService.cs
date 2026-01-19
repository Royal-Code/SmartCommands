namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain.Servicos;

public sealed class AcessoService : IAcessoService
{
    public bool UsuarioTemPermissao(Usuario usuario, string codigoPermissao)
    {
        if (!usuario.Ativo) 
            return false;

        if (string.IsNullOrWhiteSpace(codigoPermissao)) 
            return false;

        var exists = usuario.Perfis.Any(p => p.Permissoes.Any(pp => pp.Codigo.Equals(codigoPermissao)));

        return exists;
    }
}
