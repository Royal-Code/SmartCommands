using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain.ValueObjects;

public readonly record struct Email : IValidable
{
    public string Value { get; }

    public Email(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value.Trim();

        if (HasProblems(out var problems))
            throw problems.ToException();
    }

    public override string ToString() => Value;

    public static bool TryParse(string? value, out Email email)
    {
        if (value is null)
        {
            email = default;
            return false;
        }

        try
        {
            var parsedEmail = new Email(value);
            email = parsedEmail;
            return true;
        }
        catch
        {
            email = default;
            return false;
        }
    }

    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<Email>()
            .NotEmpty(Value)
            .Email(Value)
            .HasProblems(out problems);
    }
}
