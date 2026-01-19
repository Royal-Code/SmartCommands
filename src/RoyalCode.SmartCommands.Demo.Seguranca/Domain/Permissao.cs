using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain;

/// <summary>
/// Entidade de domínio que representa uma permissão de acesso.
/// </summary>
public class Permissao
{
    // campos privados

    // Construtores
    /// <summary>
    /// Cria uma permissão com código e descrição opcionais.
    /// </summary>
    public Permissao(string codigo, string? descricao = null, bool ativo = true)
    {
        Id = Guid.CreateVersion7();
        Codigo = codigo?.Trim() ?? string.Empty;
        Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        Ativo = ativo;
    }

#nullable disable
    /// <summary>
    /// Construtor protegido sem parâmetros para deserialização.
    /// </summary>
    protected Permissao() { }
#nullable enable

    // Propriedades
    /// <summary>
    /// Identificador da permissão.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Código único da permissão.
    /// </summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>
    /// Descrição opcional da permissão.
    /// </summary>
    public string? Descricao { get; private set; }

    /// <summary>
    /// Indica se a permissão está ativa.
    /// </summary>
    public bool Ativo { get; private set; }

    // Métodos
    /// <summary>
    /// Valida o estado da entidade.
    /// </summary>
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<Permissao>()
            .NotEmpty(Codigo)
            .HasProblems(out problems);
    }

    /// <summary>
    /// Ativa a permissão.
    /// </summary>
    public Result Ativar()
    {
        Ativo = true;
        return default;
    }

    /// <summary>
    /// Desativa a permissão.
    /// </summary>
    public Result Desativar()
    {
        Ativo = false;
        return default;
    }
}
