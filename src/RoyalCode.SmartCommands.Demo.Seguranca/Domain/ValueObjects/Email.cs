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

    public static implicit operator string(Email email) => email.Value;

    public static implicit operator Email(string value) => new(value);

    // ??
    public IQueryable<T> ApplyFilter<T>(IQueryable<T> query, System.Linq.Expressions.Expression<Func<T, Email>> propertyExpression)
    {
        if (string.IsNullOrWhiteSpace(Value))
            return query;

        var parameter = propertyExpression.Parameters[0];
        var body = System.Linq.Expressions.Expression.Call(
            System.Linq.Expressions.Expression.Call(
                propertyExpression.Body,
                nameof(string.ToLowerInvariant),
                Type.EmptyTypes),
            nameof(string.Contains),
            Type.EmptyTypes,
            System.Linq.Expressions.Expression.Constant(Value.ToLowerInvariant()));
        
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter);
        return query.Where(lambda);
    }

    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<Email>()
            .NotEmpty(Value)
            .Email(Value)
            .HasProblems(out problems);
    }
}
