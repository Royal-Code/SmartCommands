using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.UnitOfWork;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Tests.Components;

public class ConcurrencyRetryTests
{
    [Fact]
    public async Task RetryOnConcurrency_Must_ReturnResult_WhenBodySucceedsOnFirstAttempt()
    {
        var uow = new FakeUnitOfWork();
        var attempts = 0;

        var result = await uow.RetryOnConcurrencyAsync(
            () =>
            {
                attempts++;
                return Task.FromResult(Result.Ok());
            },
            new RetryOnConcurrencyOptions { MaxAttempts = 3 });

        Assert.False(result.HasProblems(out _));
        Assert.Equal(1, attempts);
        Assert.Equal(0, uow.CleanUpCount);
    }

    [Fact]
    public async Task RetryOnConcurrency_Must_Retry_AndSucceed_AfterTransientConflicts()
    {
        var uow = new FakeUnitOfWork();
        var attempts = 0;

        var result = await uow.RetryOnConcurrencyAsync(
            () =>
            {
                attempts++;
                if (attempts < 3)
                    throw new ConcurrencyException("conflict", new Exception());
                return Task.FromResult(Result.Ok());
            },
            new RetryOnConcurrencyOptions { MaxAttempts = 3 });

        Assert.False(result.HasProblems(out _));
        Assert.Equal(3, attempts);
        Assert.Equal(2, uow.CleanUpCount); // cleared between the two failed attempts
    }

    [Fact]
    public async Task RetryOnConcurrency_Must_ReturnInvalidState_WhenAttemptsAreExhausted()
    {
        var uow = new FakeUnitOfWork();
        var attempts = 0;

        var result = await uow.RetryOnConcurrencyAsync(
            () =>
            {
                attempts++;
                throw new ConcurrencyException("conflict", new Exception());
            },
            new RetryOnConcurrencyOptions { MaxAttempts = 3 });

        Assert.True(result.HasProblems(out var problems));
        var problem = Assert.Single(problems);
        Assert.Equal(ProblemCategory.InvalidState, problem.Category);
        Assert.Equal(ConcurrencyRetryExtensions.ConcurrencyConflictDetail, problem.Detail);
        Assert.Equal(3, attempts);
        Assert.Equal(3, uow.CleanUpCount);
    }

    [Fact]
    public async Task RetryOnConcurrency_Must_UseCustomProblem_WhenExhausted_AndOnExhaustedProvided()
    {
        var uow = new FakeUnitOfWork();

        var result = await uow.RetryOnConcurrencyAsync(
            () => throw new ConcurrencyException("conflict", new Exception()),
            new RetryOnConcurrencyOptions { MaxAttempts = 2 },
            onExhausted: () => Problems.InvalidState("custom detail", typeId: "user_account.concurrency_conflict"));

        Assert.True(result.HasProblems(out var problems));
        var problem = Assert.Single(problems);
        Assert.Equal("custom detail", problem.Detail);
        Assert.Equal("user_account.concurrency_conflict", problem.TypeId);
    }

    [Fact]
    public async Task RetryOnConcurrency_Must_RollbackTransaction_BetweenAttempts()
    {
        var transaction = new FakeTransaction();
        var uow = new FakeUnitOfWork { Transaction = transaction };
        var attempts = 0;

        var result = await uow.RetryOnConcurrencyAsync(
            () =>
            {
                attempts++;
                if (attempts < 2)
                    throw new ConcurrencyException("conflict", new Exception());
                return Task.FromResult(Result.Ok());
            },
            new RetryOnConcurrencyOptions { MaxAttempts = 3 });

        Assert.False(result.HasProblems(out _));
        Assert.Equal(1, transaction.RollbackCount); // rolled back after the single failed attempt
        Assert.Equal(1, uow.CleanUpCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1)]
    public async Task RetryOnConcurrency_Must_ExecuteOnce_WhenMaxAttemptsIsClampedToOne(int maxAttempts)
    {
        var uow = new FakeUnitOfWork();
        var attempts = 0;

        var result = await uow.RetryOnConcurrencyAsync(
            () =>
            {
                attempts++;
                throw new ConcurrencyException("conflict", new Exception());
            },
            new RetryOnConcurrencyOptions { MaxAttempts = maxAttempts });

        Assert.True(result.HasProblems(out _));
        Assert.Equal(1, attempts); // a single attempt, no retry
    }

    [Fact]
    public async Task RetryOnConcurrency_Must_StopRetrying_AfterCleanup_WhenCancellationIsRequested()
    {
        var transaction = new FakeTransaction();
        var uow = new FakeUnitOfWork { Transaction = transaction };
        var attempts = 0;
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => uow.RetryOnConcurrencyAsync(
            () =>
            {
                attempts++;
                cts.Cancel();
                throw new ConcurrencyException("conflict", new Exception());
            },
            new RetryOnConcurrencyOptions { MaxAttempts = 3 },
            ct: cts.Token));

        // cancelamento permanece cancelamento, mas só depois do cleanup da tentativa falhada
        Assert.Equal(1, attempts);
        Assert.Equal(1, transaction.RollbackCount);
        Assert.Equal(1, uow.CleanUpCount);
    }

    [Fact]
    public async Task RetryOnConcurrency_Must_Rollback_WithItsOwnToken_NotTheCancelledOne()
    {
        var transaction = new FakeTransaction();
        var uow = new FakeUnitOfWork { Transaction = transaction };
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => uow.RetryOnConcurrencyAsync(
            () =>
            {
                cts.Cancel();
                throw new ConcurrencyException("conflict", new Exception());
            },
            new RetryOnConcurrencyOptions { MaxAttempts = 3 },
            ct: cts.Token));

        // o rollback entre tentativas roda em token próprio: nunca recebe o token já cancelado
        Assert.Equal(1, transaction.RollbackCount);
        Assert.Equal(CancellationToken.None, transaction.LastRollbackToken);
    }

    [Fact]
    public async Task RetryOnConcurrency_Must_ReturnExhaustedProblem_WhenBudgetEndsOnTheCancelledAttempt()
    {
        // esgotamento na última tentativa vence a checagem de cancelamento: o chamador
        // recebe o problema de conflito, não OCE (contrato atual, travado por este teste)
        var uow = new FakeUnitOfWork();
        using var cts = new CancellationTokenSource();

        var result = await uow.RetryOnConcurrencyAsync(
            () =>
            {
                cts.Cancel();
                throw new ConcurrencyException("conflict", new Exception());
            },
            new RetryOnConcurrencyOptions { MaxAttempts = 1 },
            ct: cts.Token);

        Assert.True(result.HasProblems(out var problems));
        Assert.Equal(ProblemCategory.InvalidState, Assert.Single(problems).Category);
    }

    [Fact]
    public async Task RetryOnConcurrency_Must_PreserveBothFailures_WhenRollbackBetweenAttemptsFails()
    {
        var transaction = new FakeTransaction { ThrowOnRollback = new IOException("rollback boom") };
        var uow = new FakeUnitOfWork { Transaction = transaction };

        var aggregate = await Assert.ThrowsAsync<AggregateException>(() => uow.RetryOnConcurrencyAsync(
            () => throw new ConcurrencyException("conflict", new Exception()),
            new RetryOnConcurrencyOptions { MaxAttempts = 3 }));

        // a primeira inner é o conflito primário; a segunda é a falha do rollback
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.IsType<ConcurrencyException>(aggregate.InnerExceptions[0]);
        Assert.IsType<IOException>(aggregate.InnerExceptions[1]);
        Assert.Equal(1, uow.CleanUpCount);
    }

    [Fact]
    public async Task RetryOnConcurrencyGeneric_Must_PreserveBothFailures_AndCleanUp_WhenRollbackFails()
    {
        var transaction = new FakeTransaction { ThrowOnRollback = new IOException("rollback boom") };
        var uow = new FakeUnitOfWork { Transaction = transaction };

        var aggregate = await Assert.ThrowsAsync<AggregateException>(() => uow.RetryOnConcurrencyAsync<int>(
            () => throw new ConcurrencyException("conflict", new Exception()),
            new RetryOnConcurrencyOptions { MaxAttempts = 3 }));

        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.IsType<ConcurrencyException>(aggregate.InnerExceptions[0]);
        Assert.IsType<IOException>(aggregate.InnerExceptions[1]);
        Assert.Equal(1, uow.CleanUpCount);
    }

    [Fact]
    public async Task RetryOnConcurrency_Must_PreserveConflict_RollbackAndTrackerCleanupFailures()
    {
        var transaction = new FakeTransaction { ThrowOnRollback = new IOException("rollback boom") };
        var cleanupFailure = new InvalidOperationException("cleanup boom");
        var uow = new FakeUnitOfWork
        {
            Transaction = transaction,
            ThrowOnCleanUp = cleanupFailure,
        };

        var aggregate = await Assert.ThrowsAsync<AggregateException>(() => uow.RetryOnConcurrencyAsync(
            () => throw new ConcurrencyException("conflict", new Exception()),
            new RetryOnConcurrencyOptions { MaxAttempts = 3 }));

        Assert.Equal(3, aggregate.InnerExceptions.Count);
        Assert.IsType<ConcurrencyException>(aggregate.InnerExceptions[0]);
        Assert.IsType<IOException>(aggregate.InnerExceptions[1]);
        Assert.Same(cleanupFailure, aggregate.InnerExceptions[2]);
        Assert.Equal(1, uow.CleanUpCount);
    }

    // --- Overload generico RetryOnConcurrencyAsync<T> (ex.: ProduceNewEntity / Result<T>) ---

    [Fact]
    public async Task RetryOnConcurrencyGeneric_Must_ReturnValue_WhenBodySucceedsOnFirstAttempt()
    {
        var uow = new FakeUnitOfWork();
        var attempts = 0;

        var result = await uow.RetryOnConcurrencyAsync(
            () =>
            {
                attempts++;
                return Task.FromResult<Result<int>>(42);
            },
            new RetryOnConcurrencyOptions { MaxAttempts = 3 });

        Assert.False(result.HasProblems(out _));
        Assert.True(result.HasValue(out var value));
        Assert.Equal(42, value);
        Assert.Equal(1, attempts);
        Assert.Equal(0, uow.CleanUpCount);
    }

    [Fact]
    public async Task RetryOnConcurrencyGeneric_Must_Retry_AndReturnValue_AfterTransientConflicts()
    {
        var uow = new FakeUnitOfWork();
        var attempts = 0;

        var result = await uow.RetryOnConcurrencyAsync(
            () =>
            {
                attempts++;
                if (attempts < 3)
                    throw new ConcurrencyException("conflict", new Exception());
                return Task.FromResult<Result<int>>(7);
            },
            new RetryOnConcurrencyOptions { MaxAttempts = 3 });

        Assert.False(result.HasProblems(out _));
        Assert.True(result.HasValue(out var value));
        Assert.Equal(7, value);
        Assert.Equal(3, attempts);
        Assert.Equal(2, uow.CleanUpCount); // cleared between the two failed attempts
    }

    [Fact]
    public async Task RetryOnConcurrencyGeneric_Must_ReturnProblem_WhenExhausted_AndOnExhaustedProvided()
    {
        var uow = new FakeUnitOfWork();

        var result = await uow.RetryOnConcurrencyAsync<int>(
            () => throw new ConcurrencyException("conflict", new Exception()),
            new RetryOnConcurrencyOptions { MaxAttempts = 2 },
            onExhausted: () => Problems.InvalidState("custom detail", typeId: "user_account.concurrency_conflict"));

        Assert.True(result.HasProblems(out var problems));
        var problem = Assert.Single(problems);
        Assert.Equal("custom detail", problem.Detail);
        Assert.Equal("user_account.concurrency_conflict", problem.TypeId);
        Assert.Equal(2, uow.CleanUpCount);
    }

    [Fact]
    public async Task RetryOnConcurrencyGeneric_Must_ReturnInvalidState_WhenExhausted_WithoutOnExhausted()
    {
        var uow = new FakeUnitOfWork();

        var result = await uow.RetryOnConcurrencyAsync<int>(
            () => throw new ConcurrencyException("conflict", new Exception()),
            new RetryOnConcurrencyOptions { MaxAttempts = 2 });

        Assert.True(result.HasProblems(out var problems));
        var problem = Assert.Single(problems);
        Assert.Equal(ProblemCategory.InvalidState, problem.Category);
        Assert.Equal(ConcurrencyRetryExtensions.ConcurrencyConflictDetail, problem.Detail);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int CleanUpCount { get; private set; }

        public Exception? ThrowOnCleanUp { get; set; }

        public FakeTransaction? Transaction { get; set; }

        public void CleanUp(bool force = true)
        {
            CleanUpCount++;
            if (ThrowOnCleanUp is not null)
                throw ThrowOnCleanUp;
        }

        public ITransaction? GetCurrentTransaction() => Transaction;

        public SaveResult Save() => throw new NotSupportedException();

        public Task<SaveResult> SaveAsync(CancellationToken token = default) => throw new NotSupportedException();

        public ITransaction BeginTransaction() => throw new NotSupportedException();

        public Task<ITransaction> BeginTransactionAsync(CancellationToken token = default) => throw new NotSupportedException();
    }

    private sealed class FakeTransaction : ITransaction
    {
        public int RollbackCount { get; private set; }

        public CancellationToken? LastRollbackToken { get; private set; }

        public Exception? ThrowOnRollback { get; set; }

        public void Commit() => throw new NotSupportedException();

        public void Rollback() => throw new NotSupportedException();

        public Task CommitAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task RollbackAsync(CancellationToken ct)
        {
            if (ThrowOnRollback is not null)
                throw ThrowOnRollback;

            RollbackCount++;
            LastRollbackToken = ct;
            return Task.CompletedTask;
        }
    }
}
