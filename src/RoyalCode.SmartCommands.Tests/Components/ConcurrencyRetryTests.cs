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

        public FakeTransaction? Transaction { get; set; }

        public void CleanUp(bool force = true) => CleanUpCount++;

        public ITransaction? GetCurrentTransaction() => Transaction;

        public SaveResult Save() => throw new NotSupportedException();

        public Task<SaveResult> SaveAsync(CancellationToken token = default) => throw new NotSupportedException();

        public ITransaction BeginTransaction() => throw new NotSupportedException();

        public Task<ITransaction> BeginTransactionAsync(CancellationToken token = default) => throw new NotSupportedException();
    }

    private sealed class FakeTransaction : ITransaction
    {
        public int RollbackCount { get; private set; }

        public void Commit() => throw new NotSupportedException();

        public void Rollback() => throw new NotSupportedException();

        public Task CommitAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task RollbackAsync(CancellationToken ct)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }
    }
}
