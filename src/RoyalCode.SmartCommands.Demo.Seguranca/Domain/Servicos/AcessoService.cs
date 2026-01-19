namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain.Servicos;

public sealed class AcessoService : IAcessoService
{
    public bool UsuarioTemPermissao(Usuario usuario, string codigoPermissao, IEnumerable<Perfil> perfisDisponiveis)
    {
        if (!usuario.Ativo) return false;
        if (string.IsNullOrWhiteSpace(codigoPermissao)) return false;

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var perfilId in usuario.Perfis)
        {
            var perfil = perfisDisponiveis.FirstOrDefault(p => p.Id == perfilId && p.Ativo);
            if (perfil is null) continue;
            foreach (var p in perfil.Permissoes)
                set.Add(p);
        }

        return set.Contains(codigoPermissao.Trim());
    }
}
