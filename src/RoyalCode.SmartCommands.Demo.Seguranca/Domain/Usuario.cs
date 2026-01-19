using RoyalCode.Entities;
using RoyalCode.SmartCommands.Demo.Seguranca.Domain.ValueObjects;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain;

/// <summary>
/// Agregado de domínio que representa um usuário do sistema com controle de acesso por perfis.
/// </summary>
public class Usuario : Entity<Guid>
{
    // campos privados
    private readonly HashSet<Perfil> _perfis = [];

    // Construtores
    
    /// <summary>
    /// Cria um usuário com nome, e-mail e senha.
    /// </summary>
    public Usuario(string nome, Email email, Senha senha)
    {
        Id = Guid.CreateVersion7();
        Nome = nome?.Trim() ?? string.Empty;
        Email = email;
        Senha = senha;
        Bloqueio = new();
        Ativo = true;
    }

#nullable disable
    /// <summary>
    /// Construtor protegido sem parâmetros para deserialização.
    /// </summary>
    public Usuario() { }
#nullable enable

    // Propriedades

    /// <summary>
    /// Nome do usuário.
    /// </summary>
    public string Nome { get; private set; } = string.Empty;

    /// <summary>
    /// E-mail do usuário.
    /// </summary>
    public Email Email { get; private set; }

    /// <summary>
    /// Dados da senha do usuário.
    /// </summary>
    public Senha Senha { get; private set; }

    /// <summary>
    /// Dados de bloqueio do usuário, caso aplicável.
    /// </summary>
    public BloqueioUsuario Bloqueio { get; private set; }

    /// <summary>
    /// Indica se o usuário está ativo.
    /// </summary>
    public bool Ativo { get; private set; }

    /// <summary>
    /// Identificadores de perfis vinculados ao usuário.
    /// </summary>
    public IReadOnlyCollection<Perfil> Perfis => _perfis;

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
            .Validate(Senha)
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
        return default;
    }

    /// <summary>
    /// Altera a senha do usuário.
    /// </summary>
    public Result AlterarSenha(Senha novaSenhaHash)
    {
        if (novaSenhaHash.HasProblems(out var problems))
            return problems;

        Senha = novaSenhaHash;
        return default;
    }

    /// <summary>
    /// Ativa o usuário.
    /// </summary>
    public Result Ativar()
    {
        Ativo = true;
        return default;
    }

    /// <summary>
    /// Desativa o usuário.
    /// </summary>
    public Result Desativar()
    {
        Ativo = false;
        return default;
    }

    /// <summary>
    /// Vincula um perfil ao usuário.
    /// </summary>
    public Result VincularPerfil(Perfil perfil)
    {
        if (perfil == null)
            return Problems.InvalidParameter("perfil", "Perfil inválido.");

        // valida se já está vinculado
        if (_perfis.Any(p => p.Id == perfil.Id))
            return Problems.InvalidState("Perfil já vinculado ao usuário.")
                .With("perfilId", perfil.Id);

        _perfis.Add(perfil);
        return Result.Ok();
    }

    /// <summary>
    /// Desvincula um perfil do usuário.
    /// </summary>
    public Result DesvincularPerfil(Guid perfilId)
    {
        var perfil = _perfis.FirstOrDefault(p => p.Id == perfilId);
        if (perfil == null)
            return Problems.InvalidState("Perfil não vinculado ao usuário.")
                .With("perfilId", perfilId);

        _perfis.Remove(perfil);
        return Result.Ok();
    }
}
