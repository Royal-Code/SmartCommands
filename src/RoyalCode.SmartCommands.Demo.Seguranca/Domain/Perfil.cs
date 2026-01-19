using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain;

/// <summary>
/// Agregado de domínio que representa um perfil de acesso, composto por um conjunto de permissões.
/// </summary>
public class Perfil
{
    // campos privados
    private readonly HashSet<string> _permissoes = new(StringComparer.OrdinalIgnoreCase);

    // Construtores

    /// <summary>
    /// Cria um perfil com nome e permissões iniciais.
    /// </summary>
    public Perfil(string nome, IEnumerable<string>? permissoes = null, bool ativo = true)
    {
        Id = Guid.CreateVersion7();
        Nome = nome?.Trim() ?? string.Empty;
        Ativo = ativo;

        if (permissoes != null)
        {
            foreach (var p in permissoes)
            {
                if (!string.IsNullOrWhiteSpace(p))
                    _permissoes.Add(p.Trim());
            }
        }
    }

#nullable disable
    /// <summary>
    /// Construtor protegido sem parâmetros para deserialização.
    /// </summary>
    protected Perfil() { }
#nullable enable

    // Propriedades
    /// <summary>
    /// Identificador do perfil.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Nome do perfil.
    /// </summary>
    public string Nome { get; private set; } = string.Empty;

    /// <summary>
    /// Indica se o perfil está ativo.
    /// </summary>
    public bool Ativo { get; private set; }

    /// <summary>
    /// Coleção de códigos de permissões concedidas ao perfil.
    /// </summary>
    public IReadOnlyCollection<string> Permissoes => _permissoes;

    // Métodos
    /// <summary>
    /// Valida o estado do agregado usando <see cref="RuleSet"/>.
    /// </summary>
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<Perfil>()
            .NotEmpty(Nome)
            .HasProblems(out problems);
    }

    /// <summary>
    /// Verifica se o perfil concede uma permissão específica.
    /// </summary>
    public bool Concede(string codigoPermissao) => _permissoes.Contains(codigoPermissao);

    /// <summary>
    /// Concede uma permissão ao perfil.
    /// </summary>
    public Result Conceder(string codigoPermissao)
    {
        var valid = RuleSet.For<Perfil>()
            .NotEmpty(codigoPermissao)
            .HasProblems(out var problems);
        if (valid) return problems!;

        _permissoes.Add(codigoPermissao.Trim());
        return default;
    }

    /// <summary>
    /// Revoga uma permissão do perfil.
    /// </summary>
    public Result Revogar(string codigoPermissao)
    {
        var valid = RuleSet.For<Perfil>()
            .NotEmpty(codigoPermissao)
            .HasProblems(out var problems);
        if (valid) return problems!;

        _permissoes.Remove(codigoPermissao.Trim());
        return default;
    }

    /// <summary>
    /// Ativa o perfil.
    /// </summary>
    public Result Ativar()
    {
        Ativo = true;
        return default;
    }

    /// <summary>
    /// Desativa o perfil.
    /// </summary>
    public Result Desativar()
    {
        Ativo = false;
        return default;
    }
}
