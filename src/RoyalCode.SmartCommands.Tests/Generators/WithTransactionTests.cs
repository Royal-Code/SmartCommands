using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Generators;

/// <summary>
/// DF21: <c>[WithTransaction]</c> exige transação para o comando independentemente da opção
/// <c>BeginTransactions</c> do adapter — o handler gerado emite
/// <c>BeginAsync(requireTransaction: true, ct)</c>; sem o atributo, <c>requireTransaction: false</c>.
/// Sem unit of work, o uso produz RCCMD043 e nenhuma fonte relacionada.
/// </summary>
public class WithTransactionTests
{
    private const string Usings =
        """
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;
        using System.Threading;
        using System.Threading.Tasks;

        namespace Tests.Transactions;

        """;

    [Fact]
    public void WithTransaction_com_uow_emite_requireTransaction_true()
    {
        const string code = Usings +
            """
            public class Db : Microsoft.EntityFrameworkCore.DbContext { }

            public class DoWork
            {
                [Command, WithUnitOfWork<Db>, WithTransaction]
                public Result Execute() => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var handler = FindGeneratedSource(output, "DoWorkHandler");
        Assert.NotNull(handler);
        Assert.Contains("await this.accessor.BeginAsync(requireTransaction: true, ct);", handler);
    }

    [Fact]
    public void Sem_WithTransaction_emite_requireTransaction_false()
    {
        const string code = Usings +
            """
            public class Db : Microsoft.EntityFrameworkCore.DbContext { }

            public class DoWork
            {
                [Command, WithUnitOfWork<Db>]
                public Result Execute() => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var handler = FindGeneratedSource(output, "DoWorkHandler");
        Assert.NotNull(handler);
        Assert.Contains("await this.accessor.BeginAsync(requireTransaction: false, ct);", handler);
    }

    [Fact]
    public void WithTransaction_sem_uow_produz_RCCMD043_e_nenhuma_fonte()
    {
        const string code = Usings +
            """
            public class DoWork
            {
                [Command, WithTransaction]
                public Result Execute() => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        var reported = diagnostics.Where(d => d.Id == "RCCMD043").ToArray();
        Assert.Single(reported);
        Assert.Equal(DiagnosticSeverity.Error, reported[0].Severity);
        Assert.NotEqual(Location.None, reported[0].Location);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("DoWorkHandler"));
    }

    [Fact]
    public void WithTransaction_com_workcontext_e_retry_emite_requireTransaction_true_dentro_do_laco()
    {
        const string code =
            """
            global using System;
            global using System.Threading;
            global using System.Threading.Tasks;

            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;
            using RoyalCode.WorkContext;

            namespace Tests.Transactions;

            public class ChangeThing
            {
                [Command, WithWorkContext, WithTransaction, WithRetryOnConcurrency]
                public async Task<Result> Execute(IWorkContext context, CancellationToken ct)
                {
                    await Task.CompletedTask;
                    return Result.Ok();
                }
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var handler = FindGeneratedSource(output, "ChangeThingHandler");
        Assert.NotNull(handler);
        // a transação exigida pelo comando é recriada em cada tentativa do retry
        Assert.Contains("RetryOnConcurrencyAsync", handler);
        Assert.Contains("await this.accessor.BeginAsync(requireTransaction: true, ct);", handler);
    }

    private static string? FindGeneratedSource(Microsoft.CodeAnalysis.Compilation compilation, string logicalName)
    {
        return compilation.SyntaxTrees
            .FirstOrDefault(t =>
            {
                var name = Path.GetFileName(t.FilePath);
                return name.StartsWith($"{logicalName}.", StringComparison.Ordinal) &&
                       name.EndsWith(".g.cs", StringComparison.Ordinal);
            })
            ?.ToString();
    }
}
