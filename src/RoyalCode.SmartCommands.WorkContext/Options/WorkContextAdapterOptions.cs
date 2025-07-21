using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.WorkContext.Options;

/// <summary>
/// Options for configuring the <see cref="IWorkContext"/> adapter.
/// </summary>
public class WorkContextAdapterOptions
{
    /// <summary>
    /// <para>
    ///     Determine whether to begin transactions for commands.
    /// </para>
    /// <para>
    ///     The default value is <c>false</c>.
    ///     This means that commands will not be executed entirely in a transaction,
    ///     but will use the entity framework change tracking to save changes.
    /// </para>
    /// </summary>
    public bool BeginTransactions { get; set; }
}
