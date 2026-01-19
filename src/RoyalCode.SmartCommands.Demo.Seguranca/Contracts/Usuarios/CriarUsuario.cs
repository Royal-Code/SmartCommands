using RoyalCode.SmartCommands.Demo.Seguranca.Domain;
using RoyalCode.SmartCommands.Demo.Seguranca.Domain.ValueObjects;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Contracts.Usuarios;

/// <summary>
/// Comando para criar um novo usuário.
/// </summary>
public partial class CriarUsuario : IValidable
{
    /// <summary>
    /// O EMail do usuário.
    /// </summary>
    public Email Email { get; set; }

    /// <summary>
    /// Nome do usuário.
    /// </summary>
    public string? Nome { get; set; }

    /// <summary>
    /// Nova senha do usuário.
    /// </summary>
    public NovaSenha Senha { get; set; }

    /// <summary>
    /// Aplica as validações sobre as propriedades de entrada do comando.
    /// </summary>
    /// <param name="problems">Os problemas encontrados durante a validação.</param>
    /// <returns>Verdadeiro se houver problemas; caso contrário, falso.</returns>
    [MemberNotNullWhen(false, nameof(Nome))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CriarUsuario>()
            .Validate(Email)
            .NotEmpty(Nome)
            .Validate(Senha)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithWorkContext]
    internal Result<Usuario> Create(IWorkContext workContext, IPasswordHasher passwordHasher)
    {
        workContext.QueryAsync

        usuario.SetSenha(Senha);
        return Result.Ok(usuario);
    }
}
