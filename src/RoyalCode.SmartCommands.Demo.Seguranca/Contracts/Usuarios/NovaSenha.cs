using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Contracts.Usuarios;

/// <summary>
/// Value object de contrato para alteração de senha de usuário.
/// </summary>
public readonly struct NovaSenha : IValidable
{
    /// <summary>
    /// A senha do usuário.
    /// </summary>
    public string? Nova { get; }

    /// <summary>
    /// Confirmação da senha do usuário.
    /// </summary>
    public string? Confirma { get; }

    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<NovaSenha>()
            .NotEmpty(Nova)
            .NotEmpty(Confirma)
            .BothEqual(Nova, Confirma, StringComparison.Ordinal)
            .HasProblems(out problems);
    }
}
