using System.Collections;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using RoyalCode.SmartCommands.Generators.Generators;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Incremental;

/// <summary>
/// <para>
///     Gate estrutural da Fase 2: nenhum valor retido pelo pipeline incremental pode alcançar um
///     <see cref="ISymbol"/>, <see cref="SyntaxNode"/>, <see cref="SemanticModel"/>, <see cref="Compilation"/>,
///     <see cref="Location"/> ou <see cref="Diagnostic"/> — do contrário a <c>Compilation</c> fica viva entre
///     builds e o cache torna-se não confiável.
/// </para>
/// <para>
///     Este é um gate de regressão: descritores com símbolos podem existir somente durante a transformação;
///     as fronteiras nomeadas e retidas devem expor apenas snapshots imutáveis.
/// </para>
/// </summary>
public class PipelineRetentionTests
{
    private const string Source =
        """
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;
        using Microsoft.AspNetCore.Routing;

        namespace Tests.Scenarios.Retention;

        [MapPost("/", "create-thing")]
        public class CreateThing
        {
            public string? Name { get; set; }

            [Command]
            public Result Do() => Result.Ok();
        }

        [MapApiHandlers]
        public static partial class Endpoints { }
        """;

    [Fact]
    public void Pipeline_models_must_not_retain_roslyn_objects()
    {
        var (_, result) = Util.RunTracked(Source);
        var retainedStepNames = IncrementalGenerator.TrackingNames.RetainedModelSteps.ToHashSet();
        var trackedModelSteps = result.Results
            .SelectMany(generator => generator.TrackedSteps)
            .Where(step => retainedStepNames.Contains(step.Key))
            .ToArray();

        var actualStepNames = trackedModelSteps.Select(step => step.Key).ToHashSet();
        Assert.Contains(IncrementalGenerator.TrackingNames.Commands, actualStepNames);
        Assert.Contains(IncrementalGenerator.TrackingNames.CollectedCommands, actualStepNames);
        Assert.Contains(IncrementalGenerator.TrackingNames.MapApiHandlers, actualStepNames);
        Assert.Contains(IncrementalGenerator.TrackingNames.MapInformation, actualStepNames);
        Assert.Contains(IncrementalGenerator.TrackingNames.CollectedMapApiHandlers, actualStepNames);
        Assert.Contains(IncrementalGenerator.TrackingNames.MapApiHandlersWithMapInformation, actualStepNames);

        var retained = trackedModelSteps
            .SelectMany(step => step.Value)
            .SelectMany(run => run.Outputs)
            .Select(output => output.Value)
            .Where(value => value is not null)!;

        Assert.DoesNotContain(retained.SelectMany(Traverse), value =>
            value is ISymbol or SyntaxNode or SyntaxTree or SemanticModel or Compilation or Diagnostic or Location);
    }

    private static IEnumerable<object> Traverse(object root)
    {
        var queue = new Queue<object>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var value = queue.Dequeue();
            if (!visited.Add(value))
                continue;
            yield return value;
            if (value is string || value.GetType().IsPrimitive || value.GetType().IsEnum)
                continue;
            if (value is IEnumerable enumerable)
                foreach (var item in enumerable)
                    if (item is not null) queue.Enqueue(item);
            foreach (var field in value.GetType().GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.GetValue(value) is { } child) queue.Enqueue(child);
        }
    }
}
