using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.WorkContext;

/// <summary>
/// Resolves the <see cref="Problem"/> returned when optimistic-concurrency retry is exhausted.
/// </summary>
public interface IConcurrencyRetryProblemFactory
{
    /// <summary>
    /// Creates the problem for a command operation whose retry budget has been exhausted.
    /// </summary>
    /// <typeparam name="TCommand">The command type being retried.</typeparam>
    /// <param name="command">The command instance handled by the generated handler.</param>
    /// <param name="operation">The semantic operation key supplied by the retry attribute.</param>
    /// <returns>The problem returned to the caller.</returns>
    Problem Create<TCommand>(TCommand command, string operation);
}
