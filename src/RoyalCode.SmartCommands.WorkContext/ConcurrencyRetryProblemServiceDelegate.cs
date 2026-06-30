using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.WorkContext;

/// <summary>
/// Creates the <see cref="Problem"/> returned when optimistic-concurrency retry is exhausted, with access to DI.
/// </summary>
/// <typeparam name="TCommand">The command type being retried.</typeparam>
/// <param name="services">The service provider from the current handler scope.</param>
/// <param name="command">The command instance handled by the generated handler.</param>
/// <param name="context">The retry-exhausted operation context.</param>
/// <returns>The problem returned to the caller.</returns>
public delegate Problem ConcurrencyRetryProblemServiceDelegate<in TCommand>(
    IServiceProvider services,
    TCommand command,
    ConcurrencyRetryProblemContext context);
