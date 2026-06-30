using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.WorkContext;

/// <summary>
/// Runtime context used to create the <see cref="Problem"/> returned when optimistic-concurrency retry is exhausted.
/// </summary>
public sealed class ConcurrencyRetryProblemContext
{
    /// <summary>
    /// Creates a new context for a retry-exhausted command operation.
    /// </summary>
    /// <param name="operation">The semantic operation key supplied by the retry attribute.</param>
    /// <param name="commandType">The concrete command type being handled.</param>
    public ConcurrencyRetryProblemContext(string operation, Type commandType)
    {
        Operation = operation;
        CommandType = commandType;
    }

    /// <summary>
    /// The semantic operation key supplied by the retry attribute.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// The concrete command type being handled.
    /// </summary>
    public Type CommandType { get; }
}
