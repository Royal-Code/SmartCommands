using RoyalCode.SmartCommands.Demo.Seguranca.Domain.ValueObjects;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain;

/// <summary>
/// Agregado de domínio que representa um usuário do sistema com controle de acesso por perfis.
/// </summary>
public class Usuario
{
    // campos privados
    private readonly HashSet<Guid> _perfilIds = new();

    // Construtores
    
    /// <summary>
    /// Cria um usuário com nome, e-mail e senha.
    /// </summary>
    public Usuario(string nome, Email email, SenhaHash senhaHash, bool ativo = true)
    {
        Id = Guid.CreateVersion7();
        Nome = nome?.Trim() ?? string.Empty;
        Email = email;
        SenhaHash = senhaHash;
        Ativo = ativo;
        CriadoEm = DateTimeOffset.UtcNow;
    }

#nullable disable
    /// <summary>
    /// Construtor protegido sem parâmetros para deserialização.
    /// </summary>
    public Usuario() { }
#nullable enable

    // Propriedades
    /// <summary>
    /// Identificador do usuário.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Nome do usuário.
    /// </summary>
    public string Nome { get; private set; } = string.Empty;

    /// <summary>
    /// E-mail do usuário.
    /// </summary>
    public Email Email { get; private set; }

    /// <summary>
    /// Hash da senha do usuário.
    /// </summary>
    public SenhaHash SenhaHash { get; private set; }

    /// <summary>
    /// Indica se o usuário está ativo.
    /// </summary>
    public bool Ativo { get; private set; }

    /// <summary>
    /// Data de criação do usuário.
    /// </summary>
    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>
    /// Data de última atualização.
    /// </summary>
    public DateTimeOffset? AtualizadoEm { get; private set; }

    /// <summary>
    /// Identificadores de perfis vinculados ao usuário.
    /// </summary>
    public IReadOnlyCollection<Guid> Perfis => _perfilIds;

    // Métodos
    /// <summary>
    /// Valida o estado do agregado.
    /// </summary>
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<Usuario>()
            .NotEmpty(Nome)
            .MaxLength(Nome, 128)
            .Validate(Email)
            .Validate(SenhaHash)
            .HasProblems(out problems);
    }

    /// <summary>
    /// Altera o nome do usuário.
    /// </summary>
    public Result AlterarNome(string nome)
    {
        var has = RuleSet.For<Usuario>()
            .NotEmpty(nome)
            .MaxLength(nome, 128)
            .HasProblems(out var problems);
        if (has) return problems!;

        Nome = nome.Trim();
        AtualizadoEm = DateTimeOffset.UtcNow;
        return default;
    }

    /// <summary>
    /// Altera o e-mail do usuário.
    /// </summary>
    public Result AlterarEmail(Email email)
    {
        if (email.HasProblems(out var problems))
            return problems;

        Email = email;
        AtualizadoEm = DateTimeOffset.UtcNow;
        return default;
    }

    /// <summary>
    /// Altera a senha do usuário.
    /// </summary>
    public Result AlterarSenha(SenhaHash novaSenhaHash)
    {
        if (novaSenhaHash.HasProblems(out var problems))
            return problems;

        SenhaHash = novaSenhaHash;
        AtualizadoEm = DateTimeOffset.UtcNow;
        return default;
    }

    /// <summary>
    /// Ativa o usuário.
    /// </summary>
    public Result Ativar()
    {
        Ativo = true;
        AtualizadoEm = DateTimeOffset.UtcNow;
        return default;
    }

    /// <summary>
    /// Desativa o usuário.
    /// </summary>
    public Result Desativar()
    {
        Ativo = false;
        AtualizadoEm = DateTimeOffset.UtcNow;
        return default;
    }

    /// <summary>
    /// Vincula um perfil ao usuário.
    /// </summary>
    public Result VincularPerfil(Guid perfilId)
    {
        if (perfilId == Guid.Empty)
            return Problems.InvalidParameter("perfilId", "Perfil inválido.");

        _perfilIds.Add(perfilId);
        AtualizadoEm = DateTimeOffset.UtcNow;
        return default;
    }

    /// <summary>
    /// Desvincula um perfil do usuário.
    /// </summary>
    public Result DesvincularPerfil(Guid perfilId)
    {
        if (perfilId == Guid.Empty)
            return Problems.InvalidParameter("perfilId", "Perfil inválido.");

        _perfilIds.Remove(perfilId);
        AtualizadoEm = DateTimeOffset.UtcNow;
        return default;
    }
}
