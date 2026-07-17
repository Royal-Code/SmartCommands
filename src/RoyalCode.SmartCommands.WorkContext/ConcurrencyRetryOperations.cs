namespace RoyalCode.SmartCommands.WorkContext;

/// <summary>
/// <para>
///     Computes the default operation key used by generated handlers when
///     <c>[WithRetryOnConcurrency]</c> does not provide an explicit <c>Operation</c>.
/// </para>
/// <para>
///     The key is the namespace-qualified name of the command type (e.g.
///     <c>My.App.Commands.EditarProduto</c>): stable, culture-invariant and never a localized text.
///     Use it to register exhausted-retry problems for commands that rely on the default key.
/// </para>
/// </summary>
public static class ConcurrencyRetryOperations
{
    /// <summary>
    /// The default operation key for the command type <typeparamref name="TCommand"/>.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <returns>The namespace-qualified name of the command type.</returns>
    public static string DefaultFor<TCommand>() => DefaultFor(typeof(TCommand));

    /// <summary>
    /// The default operation key for the given command type.
    /// </summary>
    /// <param name="commandType">The command type.</param>
    /// <returns>The namespace-qualified name of the command type.</returns>
    public static string DefaultFor(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        return commandType.FullName ?? commandType.Name;
    }
}
