using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Incremental;

public class PipelineModelBehaviorTests
{
    [Fact]
    public void Invalid_candidate_reports_diagnostic_and_emits_no_related_source()
    {
        const string source =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;
            using Microsoft.AspNetCore.Routing;

            namespace Tests.Invalid;

            [MapPost("/invalid", "invalid")]
            public class InvalidCommand<T>
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [AddHandlersServices("Invalid")]
            public static partial class Services { }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(source, out var output, out var diagnostics);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "RCCMD000");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("InvalidCommandHandler"));
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("/invalid"));
    }

    [Theory]
    [InlineData("ValueTask", "Task HandleAsync", "await command.Execute(ct)")]
    [InlineData("ValueTask<Result<int>>", "Task<Result<Int32>> HandleAsync", "await command.Execute(ct)")]
    public void ValueTask_return_is_classified_and_normalized_for_handler(
        string returnType,
        string expectedSignature,
        string expectedInvocation)
    {
        var returnExpression = returnType == "ValueTask"
            ? "ValueTask.CompletedTask"
            : "ValueTask.FromResult<Result<int>>(42)";
        var source =
            $$"""
            using System.Threading;
            using System.Threading.Tasks;
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.ValueTasks;

            public class ExecuteValueTask
            {
                [Command]
                public {{returnType}} Execute(CancellationToken ct) => {{returnExpression}};
            }
            """;

        Util.Compile(source, out var output, out var generatorDiagnostics);
        var compilationErrors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        var generated = string.Join("\n", output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()));

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.True(
            compilationErrors.Length == 0,
            $"Generated sources:\n{generated}\nErrors:\n{string.Join("\n", compilationErrors.Select(d => d.ToString()))}");
        Assert.Contains(expectedSignature, generated);
        Assert.Contains(expectedInvocation, generated);
    }

    [Fact]
    public void Aggregate_outputs_are_deterministic_when_command_order_changes()
    {
        var first = CompileGenerated(Sources("Alpha", "Beta"));
        var second = CompileGenerated(Sources("Beta", "Alpha"));

        Assert.Equal(first.Keys.OrderBy(key => key), second.Keys.OrderBy(key => key));
        foreach (var key in first.Keys)
            Assert.Equal(first[key], second[key]);
    }

    private static Dictionary<string, string> CompileGenerated(string source)
    {
        Util.Compile(source, out var output, out var diagnostics);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        return output.SyntaxTrees.Skip(1).ToDictionary(tree => tree.FilePath, tree => tree.ToString());
    }

    private static string Sources(string first, string second) =>
        $$"""
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;
        using Microsoft.AspNetCore.Routing;

        namespace Tests.Determinism;

        [MapPost("/{{first.ToLowerInvariant()}}", "{{first}}")]
        public class {{first}}
        {
            [Command]
            public Result Execute() => Result.Ok();
        }

        [MapPost("/{{second.ToLowerInvariant()}}", "{{second}}")]
        public class {{second}}
        {
            [Command]
            public Result Execute() => Result.Ok();
        }

        [AddHandlersServices("Tests")]
        public static partial class Services { }

        [MapApiHandlers]
        public static partial class Endpoints { }
        """;
}
