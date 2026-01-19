using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain.ValueObjects;

/// <summary>
/// Value object que representa um hash de senha.
/// </summary>
public readonly record struct SenhaHash(string Value) : IValidable
{
    /// <summary>
    /// Retorna o valor do hash da senha.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Valida o hash da senha usando <see cref="RuleSet"/>.
    /// </summary>
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<SenhaHash>()
            .NotEmpty(Value)
            .MinLength(Value, 20)
            .HasProblems(out problems);
    }
}
