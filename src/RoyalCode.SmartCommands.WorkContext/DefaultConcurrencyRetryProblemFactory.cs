using Microsoft.Extensions.Options;
using RoyalCode.SmartCommands.WorkContext.Internals;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.WorkContext;

internal sealed class DefaultConcurrencyRetryProblemFactory(
    IServiceProvider services,
    IEnumerable<IConcurrencyRetryProblemRegistration> registrations,
    IOptions<RetryOnConcurrencyOptions> options)
    : IConcurrencyRetryProblemFactory
{
    /// <inheritdoc />
    public Problem Create<TCommand>(TCommand command, string operation)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("The operation must not be empty.", nameof(operation));

        var commandType = command.GetType();
        var context = new ConcurrencyRetryProblemContext(operation, commandType);
        var registration = FindRegistration(commandType, operation);

        if (registration is not null)
            return registration.Create(services, command, context);

        var value = options.Value;
        var detail = value.ExhaustedProblemDetail
            ?? RoyalCode.WorkContext.ConcurrencyRetryExtensions.ConcurrencyConflictDetail;

        return Problems.InvalidState(detail, typeId: value.ExhaustedProblemTypeId);
    }

    private IConcurrencyRetryProblemRegistration? FindRegistration(Type commandType, string operation)
    {
        var operationRegistrations = registrations
            .Where(r => string.Equals(r.Operation, operation, StringComparison.Ordinal))
            .ToArray();

        return operationRegistrations.FirstOrDefault(r => r.CommandType == commandType)
            ?? operationRegistrations.FirstOrDefault(r => r.CommandType != typeof(object)
                && r.CommandType.IsAssignableFrom(commandType))
            ?? operationRegistrations.FirstOrDefault(r => r.CommandType == typeof(object));
    }
}
