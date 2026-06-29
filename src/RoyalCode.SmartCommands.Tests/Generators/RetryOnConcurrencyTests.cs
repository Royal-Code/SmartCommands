using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Generators;

public class RetryOnConcurrencyTests
{
    [Fact]
    public void WithRetryOnConcurrency_NoArgument_WrapsBody_AndReadsAttemptsFromOptions()
    {
        Util.Compile(Code.CommandWithOptions, out var output, out var diagnostics);

        AssertNoErrors(diagnostics);
        AssertNoErrors(output.GetDiagnostics());

        var generatedHandler = FindGeneratedSource(output, "ChangePasswordHandler.g.cs");
        Assert.Equal(Normalize(Code.HandlerWithOptions), Normalize(generatedHandler));
    }

    [Fact]
    public void WithRetryOnConcurrency_WithExplicitValue_WrapsBody_AndUsesInlineOptions()
    {
        Util.Compile(Code.CommandWithValue, out var output, out var diagnostics);

        AssertNoErrors(diagnostics);
        AssertNoErrors(output.GetDiagnostics());

        var generatedHandler = FindGeneratedSource(output, "ChangePasswordHandler.g.cs");
        Assert.Equal(Normalize(Code.HandlerWithValue), Normalize(generatedHandler));
    }

    private static string? FindGeneratedSource(Compilation compilation, string fileName)
    {
        return compilation.SyntaxTrees
            .FirstOrDefault(t => Path.GetFileName(t.FilePath).Equals(fileName, StringComparison.Ordinal))
            ?.ToString();
    }

    private static void AssertNoErrors(IEnumerable<Diagnostic> diagnostics)
    {
        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(d => d.ToString())));
    }

    // snapshot comparison must be independent of line endings (raw strings may be LF, generator emits CRLF)
    private static string? Normalize(string? value) => value?.Replace("\r\n", "\n");

    [Fact]
    public void WithRetryOnConcurrency_WithoutWorkContext_ReportsDiagnostic()
    {
        Util.Compile(Code.CommandWithoutWorkContext, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD024" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void WithRetryOnConcurrency_WithNonPositiveValue_ReportsDiagnostic()
    {
        Util.Compile(Code.CommandInvalidMax, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD025" && d.Severity == DiagnosticSeverity.Error);
    }
}

file static class Code
{
    public const string CommandWithOptions =
"""
global using System;
global using System.Threading;
global using System.Threading.Tasks;

using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace Tests.Scenarios.Retry;

public class ChangePassword
{
    [Command, WithWorkContext, WithRetryOnConcurrency]
    public async Task<Result> Execute(IWorkContext context, CancellationToken ct)
    {
        await Task.CompletedTask;
        return Result.Ok();
    }
}
""";

    public const string HandlerWithOptions =
"""
using Microsoft.Extensions.Options;
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;
using Tests.Scenarios.Retry;

namespace Tests.Scenarios.Retry.Internals;

public class ChangePasswordHandler<TContext> : IChangePasswordHandler
    where TContext : IWorkContext
{
    private readonly IUnitOfWorkAccessor<TContext> accessor;
    private readonly IOptions<RetryOnConcurrencyOptions> retryOptions;

    public ChangePasswordHandler(IUnitOfWorkAccessor<TContext> accessor, IOptions<RetryOnConcurrencyOptions> retryOptions)
    {
        this.accessor = accessor;
        this.retryOptions = retryOptions;
    }

    public async Task<Result> HandleAsync(ChangePassword command, CancellationToken ct)
    {
        return await this.accessor.Context.RetryOnConcurrencyAsync(
            async () =>
            {
                await this.accessor.BeginAsync(ct);

                return await command.Execute(this.accessor.Context, ct).ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
            },
            this.retryOptions.Value,
            ct: ct);
    }
}

""";

    public const string CommandWithValue =
"""
global using System;
global using System.Threading;
global using System.Threading.Tasks;

using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace Tests.Scenarios.Retry;

public class ChangePassword
{
    [Command, WithWorkContext, WithRetryOnConcurrency(5)]
    public async Task<Result> Execute(IWorkContext context, CancellationToken ct)
    {
        await Task.CompletedTask;
        return Result.Ok();
    }
}
""";

    public const string HandlerWithValue =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;
using Tests.Scenarios.Retry;

namespace Tests.Scenarios.Retry.Internals;

public class ChangePasswordHandler<TContext> : IChangePasswordHandler
    where TContext : IWorkContext
{
    private readonly IUnitOfWorkAccessor<TContext> accessor;

    public ChangePasswordHandler(IUnitOfWorkAccessor<TContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result> HandleAsync(ChangePassword command, CancellationToken ct)
    {
        return await this.accessor.Context.RetryOnConcurrencyAsync(
            async () =>
            {
                await this.accessor.BeginAsync(ct);

                return await command.Execute(this.accessor.Context, ct).ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
            },
            new RetryOnConcurrencyOptions { MaxAttempts = 5 },
            ct: ct);
    }
}

""";

    public const string CommandWithoutWorkContext =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Retry;

public class ChangePassword
{
    [Command, WithRetryOnConcurrency]
    public Result Execute()
    {
        return Result.Ok();
    }
}
""";

    public const string CommandInvalidMax =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;

namespace Tests.Scenarios.Retry;

public class ChangePassword
{
    [Command, WithWorkContext, WithRetryOnConcurrency(0)]
    public async Task<Result> Execute(IWorkContext context, CancellationToken ct)
    {
        await Task.CompletedTask;
        return Result.Ok();
    }
}
""";
}
