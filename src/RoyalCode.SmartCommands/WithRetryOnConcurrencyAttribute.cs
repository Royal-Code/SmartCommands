using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Opt-in attribute that makes the generated command handler retry the command body on optimistic-concurrency
///     conflicts. It is only supported together with <see cref="WithWorkContextAttribute"/>.
/// </para>
/// <para>
///     When applied, the generated handler wraps <c>{ Begin → find entities → Execute → Complete }</c> in a retry
///     loop: a concurrency conflict rolls back the transaction (if any), clears the change tracker and re-runs the
///     body with fresh state, up to the configured number of attempts. The model validation stays outside the loop.
/// </para>
/// <para>
///     With no argument, the number of attempts comes from the configured <c>RetryOnConcurrencyOptions.MaxAttempts</c>
///     (deployment level). With an explicit <c>maxAttempts</c>, that value is used instead.
/// </para>
/// <para>
///     When <see cref="Operation"/> is provided, the generated handler asks the registered retry problem factory
///     to create the problem returned when the retry budget is exhausted.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public sealed class WithRetryOnConcurrencyAttribute : Attribute
{
    /// <summary>
    /// Uses the number of attempts from the configured <c>RetryOnConcurrencyOptions</c>.
    /// </summary>
    public WithRetryOnConcurrencyAttribute() { }

    /// <summary>
    /// Uses a fixed number of attempts, overriding the configured options.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of attempts (initial execution plus retries). Must be greater than zero.</param>
    public WithRetryOnConcurrencyAttribute(int maxAttempts) { }

    /// <summary>
    /// Uses the configured number of attempts and the specified operation key for exhausted retry problems.
    /// </summary>
    /// <param name="operation">The semantic operation key used by the retry problem factory.</param>
    public WithRetryOnConcurrencyAttribute(string operation) { }

    /// <summary>
    /// Uses a fixed number of attempts and the specified operation key for exhausted retry problems.
    /// </summary>
    /// <param name="operation">The semantic operation key used by the retry problem factory.</param>
    /// <param name="maxAttempts">The maximum number of attempts (initial execution plus retries). Must be greater than zero.</param>
    public WithRetryOnConcurrencyAttribute(string operation, int maxAttempts) { }

    /// <summary>
    /// The semantic operation key used by the retry problem factory when the retry budget is exhausted.
    /// </summary>
    public string? Operation { get; set; }
}
