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

    public async Task<Result> ValidateAsync(IWorkContext workContext, CancellationToken ct)
    {
        var emailExiste = await workContext.Criteria<Usuario>().FilterBy(new UsuarioFiltro
        {
            Email = Email.Value
        }).ExistsAsync(ct);
        
        if (emailExiste)
        {
            return Problems.InvalidParameter("E-Mail já cadastrado para outro usuário", "Email")
                .With("email", Email.Value);
        }

        return Result.Ok();
    }

    [Command, WithValidateModel, WithWorkContext]
    internal async Task<Result<Usuario>> Create(IWorkContext workContext, IPasswordHasher passwordHasher, CancellationToken ct)
    {
        WasValidated();
        var validationResult = await ValidateAsync(workContext, ct);
        if (validationResult.HasProblems(out var problems))
            return problems;

        Senha senha = passwordHasher.Hash(Senha.Nova!);

        return new Usuario(Nome, Email, senha);
    }
}
