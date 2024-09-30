using RoyalCode.SmartProblems;

namespace Coreum.NewCommands;

/// <summary>
/// <para>
///     A service that provides access to the unit of work.
/// </para>
/// </summary>
/// <typeparam name="T">The type of the context of the unit of work.</typeparam>
public interface IUnitOfWorkAccessor<out T> : IRepositoriesAccessor<T>
    where T : class
{
    /// <summary>
    /// Invoked when the unit of work is about to begin.
    /// </summary>
    /// <returns></returns>
    public ValueTask BeginAsync(CancellationToken ct);

    /// <summary>
    /// <para>
    ///     Invoked when the unit of work is about to complete.
    /// </para>
    /// <para>
    ///     At this point, the unit of work should be committed (or the save changes should be called).
    /// </para>
    /// </summary>
    /// <returns>Tje result of the operation.</returns>
    public Task<Result> CompleteAsync(CancellationToken ct);
}