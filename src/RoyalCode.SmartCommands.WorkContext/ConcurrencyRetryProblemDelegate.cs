using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.WorkContext;

/// <summary>
/// Creates the <see cref="Problem"/> returned when optimistic-concurrency retry is exhausted.
/// </summary>
/// <typeparam name="TCommand">The command type being retried.</typeparam>
/// <param name="command">The command instance handled by the generated handler.</param>
/// <param name="context">The retry-exhausted operation context.</param>
/// <returns>The problem returned to the caller.</returns>
public delegate Problem ConcurrencyRetryProblemDelegate<in TCommand>(
    TCommand command,
    ConcurrencyRetryProblemContext context);
