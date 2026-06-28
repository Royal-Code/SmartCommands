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
///     (deployment level). With an explicit <paramref name="maxAttempts"/>, that value is used instead.
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
}
