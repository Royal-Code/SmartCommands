namespace RoyalCode.SmartCommands.WorkContext.Options;

/// <summary>
/// <para>
///     Options for the optimistic-concurrency retry primitive applied around unit-of-work command bodies.
/// </para>
/// <para>
///     These options are meant to be configured at deployment level (e.g. appsettings) and are read by the
///     command handlers generated for commands annotated with the retry attribute, unless a specific value
///     is provided on the attribute itself.
/// </para>
/// </summary>
public class RetryOnConcurrencyOptions
{
    /// <summary>
    /// The default number of attempts (<c>3</c>) used when nothing is configured.
    /// </summary>
    public const int DefaultMaxAttempts = 3;

    /// <summary>
    /// The configuration section bound to these options (<c>"RetryOnConcurrency"</c>).
    /// </summary>
    public const string ConfigurationSectionName = "RetryOnConcurrency";

    /// <summary>
    /// <para>
    ///     The maximum number of attempts (the initial execution plus retries) for a command body
    ///     under optimistic-concurrency conflicts.
    /// </para>
    /// <para>
    ///     The default value is <see cref="DefaultMaxAttempts"/> (3), without backoff: market practice for
    ///     optimistic-concurrency retry is a small, immediate retry budget (the next attempt simply reloads and
    ///     re-applies). Do not confuse it with transient-failure retry (e.g. EF <c>EnableRetryOnFailure</c>).
    /// </para>
    /// <para>
    ///     Values lower than <c>1</c> are treated as <c>1</c> (a single attempt, no retry).
    /// </para>
    /// </summary>
    public int MaxAttempts { get; set; } = DefaultMaxAttempts;

    /// <summary>
    /// Optional problem type id used by the default retry-exhausted problem factory.
    /// </summary>
    public string? ExhaustedProblemTypeId { get; set; }

    /// <summary>
    /// Optional detail used by the default retry-exhausted problem factory.
    /// </summary>
    public string? ExhaustedProblemDetail { get; set; }
}
