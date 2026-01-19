using RoyalCode.Entities;
using RoyalCode.SmartProblems;
using RoyalCode.SmartSearch.Core.Extensions;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain;

/// <summary>
/// Agregado de domínio que representa um perfil de acesso, composto por um conjunto de permissões.
/// </summary>
public class Perfil : Entity<Guid>
{
    // campos privados
    private readonly List<Permissao> _permissoes = new();

    // Construtores

    /// <summary>
    /// Cria um perfil com nome e permissões iniciais.
    /// </summary>
    public Perfil(string nome, IEnumerable<Permissao>? permissoes = null, bool ativo = true)
    {
        Id = Guid.CreateVersion7();
        Nome = nome?.Trim() ?? string.Empty;
        Ativo = ativo;

        if (permissoes is not null)
            _permissoes.AddRange(permissoes);
    }

#nullable disable
    /// <summary>
    /// Construtor protegido sem parâmetros para deserialização.
    /// </summary>
    protected Perfil() { }
#nullable enable

    // Propriedades

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
    public IReadOnlyCollection<Permissao> Permissoes => _permissoes;

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
    /// Concede uma permissão ao perfil.
    /// </summary>
    public Result Conceder(Permissao codigoPermissao)
    {
        // valida se já foi concedida
        if (_permissoes.Any(p => p.Codigo == codigoPermissao.Codigo))
        {
            return Problems.InvalidState(
                $"Permissão '{codigoPermissao.Codigo}' já concedida ao perfil '{Nome}'.");
        }

        _permissoes.Add(codigoPermissao);
        return Result.Ok();
    }

    /// <summary>
    /// Verifica se o perfil concede uma permissão específica.
    /// </summary>
    public bool Concede(string codigoPermissao)
    {
        if (codigoPermissao.IsEmpty())
        {
            return false;
        }

        return _permissoes.Any(p => p.Codigo == codigoPermissao.Trim());
    }

    /// <summary>
    /// Revoga uma permissão do perfil.
    /// </summary>
    public Result Revogar(string codigoPermissao)
    {
        var permissão = _permissoes.FirstOrDefault(p => p.Codigo == codigoPermissao.Trim());
        if (permissão is null)
        {
            return Problems.InvalidState(
                $"Permissão '{codigoPermissao}' não concedida ao perfil '{Nome}'.");
        }

        _permissoes.Remove(permissão);
        return Result.Ok();
    }

    /// <summary>
    /// Revoga uma permissão do perfil.
    /// </summary>
    public Result Revogar(Guid permissaoId)
    {
        var permissão = _permissoes.FirstOrDefault(p => p.Id == permissaoId);
        if (permissão is null)
        {
            return Problems.InvalidState(
                $"Permissão '{permissaoId}' não concedida ao perfil '{Nome}'.");
        }

        _permissoes.Remove(permissão);
        return Result.Ok();
    }

    /// <summary>
    /// Ativa o perfil.
    /// </summary>
    public Result Ativar()
    {
        if (Ativo)
            return Problems.InvalidState($"Perfil '{Nome}' já está ativo.");

        Ativo = true;
        return Result.Ok();
    }

    /// <summary>
    /// Desativa o perfil.
    /// </summary>
    public Result Desativar()
    {
        if (!Ativo)
            return Problems.InvalidState($"Perfil '{Nome}' já está desativado.");   

        Ativo = false;
        return Result.Ok();
    }
}
