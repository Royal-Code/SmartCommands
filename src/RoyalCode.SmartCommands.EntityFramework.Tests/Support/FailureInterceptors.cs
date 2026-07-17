using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RoyalCode.SmartCommands.EntityFramework.Tests.Support;

/// <summary>
/// Double de falha do save: quando <see cref="Throw"/> está definido, o <c>SaveChangesAsync</c>
/// lança exatamente aquela instância, permitindo asserções de identidade (stack/causa preservados).
/// </summary>
public sealed class SaveFailureInterceptor : SaveChangesInterceptor
{
    public Exception? Throw { get; set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (Throw is not null)
            throw Throw;

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

/// <summary>
/// Double de falha e observação de transações: injeta falhas em commit/rollback, conta as
/// operações efetivadas e preserva a última transação física para verificar seu descarte.
/// </summary>
public sealed class TransactionFailureInterceptor : DbTransactionInterceptor
{
    public Exception? ThrowOnCommit { get; set; }

    public Exception? ThrowOnRollback { get; set; }

    public int CommitCount { get; private set; }

    public int RollbackCount { get; private set; }

    public DbTransaction? LastStartedTransaction { get; private set; }

    public void ResetCounters()
    {
        CommitCount = 0;
        RollbackCount = 0;
        LastStartedTransaction = null;
    }

    public override DbTransaction TransactionStarted(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result)
    {
        LastStartedTransaction = result;
        return result;
    }

    public override ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        LastStartedTransaction = result;
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnCommit is not null)
            throw ThrowOnCommit;

        return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }

    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        CommitCount++;
        return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }

    public override ValueTask<InterceptionResult> TransactionRollingBackAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnRollback is not null)
            throw ThrowOnRollback;

        return base.TransactionRollingBackAsync(transaction, eventData, result, cancellationToken);
    }

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        RollbackCount++;
        return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
    }
}
